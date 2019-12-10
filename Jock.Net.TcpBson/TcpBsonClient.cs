﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jock.Net.TcpBson
{
    /// <summary>
    /// Bson Communication Client
    /// </summary>
    public class TcpBsonClient : SafeThreadObject
    {
        private IPEndPoint remoteEP;
        private TcpBsonConnectionDeniedEventHandler connectionDeniedEventHandler;
        private Queue<TcpBsonPackage> sendPackageQueue = new Queue<TcpBsonPackage>();
        private MemoryStream readCache = new MemoryStream();
        private DateTime mRemoteActiveTime;
        private List<Action<string>> mCommandHandlers = new List<Action<string>>();
        private List<Action<byte[], TcpBsonClient>> mBytesHandlers = new List<Action<byte[], TcpBsonClient>>();
        private List<JsonCallback> mJsonHandlers = new List<JsonCallback>();
        private List<Action<TcpBsonClient>> mStopHandlers = new List<Action<TcpBsonClient>>();
        private List<TcpBsonNamedStream> mNamedStreams = new List<TcpBsonNamedStream>();
        private List<TcpBsonRequestContext> mWaitResponseContexts = new List<TcpBsonRequestContext>();
        private List<ReceiveRequestCallback> mReceiveRequestCallbacks = new List<ReceiveRequestCallback>();

        /// <summary>
        /// Create a <c>TcpBsonClient</c> with out <c>IPEndPoint</c>.
        /// Must start by `Start(IPEndPoint remoteEP)` method.
        /// </summary>
        public TcpBsonClient()
        {
        }

        /// <summary>
        /// Start core thread
        /// </summary>
        /// <param name="remoteEP">the Endpoint you want connect</param>
        /// <param name="connectionDeniedEventHandler">a eventhandler invoke if connection denied</param>
        /// <exception cref="ArgumentNullException">remoteEP and connectionDeniedEventHandler can not null</exception>
        /// <exception cref="NotSupportedException"></exception>
        public void Start(IPEndPoint remoteEP, TcpBsonConnectionDeniedEventHandler connectionDeniedEventHandler)
        {
            if (Client != null)
            {
                throw new NotSupportedException("Client has init by Construct method, invoke No parameters `Start()`");
            }
            this.remoteEP = remoteEP ?? throw new ArgumentNullException(nameof(remoteEP));
            this.connectionDeniedEventHandler = connectionDeniedEventHandler ?? throw new ArgumentNullException(nameof(connectionDeniedEventHandler));
            Client = new TcpClient();
            Start();
        }

        /// <summary>
        /// Connect to the specified service side
        /// </summary>
        /// <param name="remoteEP">The service endpoint to connect to</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="SocketException"></exception>
        public TcpBsonClient(IPEndPoint remoteEP)
        {
            Client = new TcpClient();
            Client.Connect(this.remoteEP = remoteEP);
            this.mRemoteActiveTime = DateTime.Now;
        }

        internal TcpBsonClient(TcpClient tcpClient)
        {
            this.Client = tcpClient;
            this.mRemoteActiveTime = DateTime.Now;
        }

        /// <summary>
        /// Gets a named Stream object
        /// </summary>
        /// <param name="name">Stream name</param>
        /// <returns>Named stream</returns>
        public TcpBsonNamedStream GetNamedStream(string name)
        {
            lock (mNamedStreams)
            {
                var found = mNamedStreams.FirstOrDefault(s => s.Name == name);
                if (found != null)
                {
                    return found;
                }
                mNamedStreams.Add(found = new TcpBsonNamedStream(this) { Name = name });
                return found;
            }
        }

        internal void FlushNamedStream(TcpBsonNamedStream TcpBsonNamedStream)
        {
            var buffer = TcpBsonNamedStream.GetWriteCacheAndClear();
            if (buffer.Length > 0)
            {
                var package = new TcpBsonPackage
                {
                    Type = TcpBsonPackageType.NamedStream,
                    DataType = TcpBsonNamedStream.Name,
                    DataBytes = buffer
                };
                SendPackage(package);
            }
        }

        /// <summary>
        /// The associated <c>TcpClient</c> object
        /// </summary>
        public TcpClient Client { get; private set; }

        /// <summary>
        /// Interval for automatic Ping
        /// </summary>
        public TimeSpan PingTimeSpan { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Correlation variable Pool, Hot any value in local <c>TcpBsonClient</c>
        /// </summary>
        public Dictionary<string, object> Session { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private TcpBsonCookies mCookes;

        /// <summary>
        /// Cookies what can auto sync between two connected <c>TcpBsonClient</c>
        /// </summary>
        public TcpBsonCookies Cookies
        {
            get
            {
                if (mCookes != null)
                {
                    return mCookes;
                }
                lock (this)
                {
                    if (mCookes == null)
                    {
                        mCookes = new TcpBsonCookies(this);
                    }
                }
                return mCookes;
            }
        }

        /// <summary>
        /// Internal thread Run code
        /// </summary>
        /// <param name="token">Triggering a cancellation notification when the user calls the Stop method</param>
        protected override void DoRun(CancellationToken token)
        {
            if (!Client.Connected && remoteEP != null)
            {
                RECONN:
                try
                {
                    Client.Connect(remoteEP);
                }
                catch (ObjectDisposedException)
                {
                    Client = new TcpClient();
                    goto RECONN;
                }
                catch (SocketException)
                {
                    var e = new TcpBsonConnectionDeniedEventArgs(this);
                    connectionDeniedEventHandler(e);
                    if (e.Retry)
                    {
                        goto RECONN;
                    }
                    return;
                }
            }
            using (var stream = Client.GetStream())
            {
                while (!token.IsCancellationRequested)
                {
                    // System.Diagnostics.Trace.WriteLine($"{Thread.CurrentThread.Name} - DoRun");
                    var wait = true;
                    if (stream.DataAvailable)
                    {
                        ReadData(stream);
                        wait = false;
                    }
                    lock (sendPackageQueue)
                    {
                        while (sendPackageQueue.Count > 0)
                        {
                            SendPackageInner(stream, sendPackageQueue.Dequeue());
                            wait = false;
                        }
                    }
                    if (wait)
                    {
                        Thread.Sleep(50);
                    }
                    if (DateTime.Now - mRemoteActiveTime > PingTimeSpan)
                    {
                        SendPackageInner(stream, new TcpBsonPackage { Type = TcpBsonPackageType.Ping });
                        mRemoteActiveTime = DateTime.Now;
                    }
                }
            }
        }

        private void ReadData(NetworkStream stream)
        {
            var size = Client.Available;
            var buffer = new byte[size];
            var readed = stream.Read(buffer, 0, size);
            readCache.Write(buffer, 0, readed);
            TryReadPackages();
        }

        private void TryReadPackages()
        {
            TryNext:
            if (readCache.Length < 5)
            {
                return;
            }
            var buffer = readCache.ToArray();
            var type = (TcpBsonPackageType)buffer[0];
            var size = BitConverter.ToInt32(buffer, 1);
            if (buffer.Length >= size + 5)
            {
                var bodyBuffer = new byte[size];
                Array.Copy(buffer, 5, bodyBuffer, 0, size);
                using (var reader = new BinaryReader(new MemoryStream(bodyBuffer)))
                {
                    var package = new TcpBsonPackage();
                    package.Type = type;
                    package.DataType = reader.ReadString();
                    var byteSize = reader.ReadInt32();
                    package.DataBytes = reader.ReadBytes(byteSize);
                    Recive(package);
                }
                readCache = new MemoryStream();
                if (buffer.Length > size + 5)
                {
                    readCache.Write(buffer, size + 5, buffer.Length - size - 5);
                    readCache.Position = 0;
                }
                goto TryNext;
            }
        }

        private void Recive(TcpBsonPackage package)
        {
            switch (package.Type)
            {
                case TcpBsonPackageType.Command:
                    DoReceiveCommand(Encoding.UTF8.GetString(package.DataBytes));
                    break;
                case TcpBsonPackageType.Bson:
                    DoReceiveBson(package);
                    break;
                case TcpBsonPackageType.Ping:
                    DoPing();
                    break;
                case TcpBsonPackageType.NamedStream:
                    DoReceiveNamedStream(package);
                    break;
                case TcpBsonPackageType.Bytes:
                    DoReceiveBytes(package);
                    break;
                case TcpBsonPackageType.Request:
                    DoReceiveRequest(package);
                    break;
                case TcpBsonPackageType.Response:
                    DoReceiveResponse(package);
                    break;
                case TcpBsonPackageType.CookieSync:
                    Cookies.Sync(package);
                    break;
                default:
                    break;
            }
        }

        private void DoReceiveResponse(TcpBsonPackage package)
        {
            if (package.DataType == "ERROR")
            {
                var errorResponse = BsonConvert.DeserializeObject<TcpBsonResponse<string>>(package.DataBytes);
                var responseCxt = mWaitResponseContexts.FirstOrDefault(r => r.Id == errorResponse.Id);
                if (responseCxt != null)
                {
                    responseCxt.SetError(errorResponse.Object);
                }
            }
            else
            {
                var responseType = typeof(TcpBsonResponse<>).MakeGenericType(Type.GetType(package.DataType));
                var responseBody = BsonConvert.DeserializeObject(package.DataBytes, responseType);
                var id = (Guid)responseType.GetProperty("Id").GetValue(responseBody, null);
                var responseCxt = mWaitResponseContexts.FirstOrDefault(r => r.Id == id);
                if (responseCxt != null)
                {
                    responseCxt.SetResponse(responseType.GetProperty("Object").GetValue(responseBody, null));
                }
            }
        }

        private void DoReceiveRequest(TcpBsonPackage package)
        {
            var requestType = Type.GetType(package.DataType);
            var dataType = typeof(TcpBsonRequest<>).MakeGenericType(requestType);

            var requestBody = BsonConvert.DeserializeObject(package.DataBytes, dataType) as TcpBsonRequest;
            var handler = mReceiveRequestCallbacks.FirstOrDefault(c => c.URI.Equals(requestBody.URI, StringComparison.OrdinalIgnoreCase) && (c.RequestType == requestType || c.RequestType.IsAssignableFrom(requestType)));

            var responsePackage = new TcpBsonPackage { Type = TcpBsonPackageType.Response };
            object responseBody = null;
            if (handler == null)
            {
                responseBody = new TcpBsonResponse<string> { Id = requestBody.Id, Object = "No handle this request" };
                responsePackage.DataType = "ERROR";
                return;
            }
            try
            {
                var response = handler.Callback.DynamicInvoke(requestBody.Object, this);
                var responseDataType = response != null ? response.GetType() : handler.ResponseType;
                var responseType = typeof(TcpBsonResponse<>).MakeGenericType(responseDataType);

                var responseData = responseType.GetConstructor(Type.EmptyTypes).Invoke(null);
                responseType.GetProperty("Id").SetValue(responseData, requestBody.Id, null);
                responseType.GetProperty("Object").SetValue(responseData, response, null);

                responsePackage.DataType = responseDataType.AssemblyQualifiedName;
                responseBody = responseData;
            }
            catch (Exception e)
            {
                responseBody = new TcpBsonResponse<string> { Id = requestBody.Id, Object = "Handle Exception - " + e.ToString() };
                responsePackage.DataType = "ERROR";
            }
            finally
            {
                responsePackage.DataBytes = BsonConvert.SerializeObject(responseBody);
                SendPackage(responsePackage);
            }
        }

        private void DoReceiveBytes(TcpBsonPackage package)
        {
            mBytesHandlers.ForEach(callback => callback(package.DataBytes, this));
        }

        private void DoReceiveNamedStream(TcpBsonPackage package)
        {
            var namedStream = GetNamedStream(package.DataType);
            namedStream.OnDataReceive(package.DataBytes);
        }

        private void DoPing()
        {
            mRemoteActiveTime = DateTime.Now;
        }

        private void DoReceiveBson(TcpBsonPackage package)
        {
            var dataType = Type.GetType(package.DataType);
            lock (mJsonHandlers)
            {
                foreach (var handler in mJsonHandlers)
                {
                    if (handler.Type == dataType)
                    {
                        handler.Callback.DynamicInvoke(BsonConvert.DeserializeObject(package.DataBytes, dataType), this);
                    }
                }
            }
        }

        private void DoReceiveCommand(string cmd)
        {
            lock (mCommandHandlers)
            {
                foreach (var handler in mCommandHandlers)
                {
                    handler.Invoke(cmd);
                }
            }
        }

        private void SendPackageInner(NetworkStream stream, TcpBsonPackage TcpBsonPackage)
        {
            var buffer = TcpBsonPackage.ToBytes();
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
            TcpBsonPackage.Callback?.Invoke();
        }

        internal void SendPackage(TcpBsonPackage package)
        {
            sendPackageQueue.Enqueue(package);
        }

        /// <summary>
        /// Send a command
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="callback">Post-Send callback</param>
        public void SendCommand(string command, Action callback = null)
        {
            SendPackage(new TcpBsonPackage
            {
                Type = TcpBsonPackageType.Command,
                DataBytes = Encoding.UTF8.GetBytes(command),
                Callback = callback
            });
        }

        /// <summary>
        /// Send an Object
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="obj">Object</param>
        /// <param name="callback">Post-Send callback</param>
        public void SendObject<T>(T obj, Action callback = null)
        {
            SendPackage(new TcpBsonPackage
            {
                Type = TcpBsonPackageType.Bson,
                DataType = GetDataType(typeof(T)),
                DataBytes = BsonConvert.SerializeObject(obj),
                Callback = callback
            });
        }

        /// <summary>
        /// Sync Send an Request and wait Response
        /// </summary>
        /// <typeparam name="TRequest">Response Object Type</typeparam>
        /// <param name="uri">A request uri</param>
        /// <param name="obj">Request object</param>
        /// <param name="millisecondsTimeout">Response timeout milliseconds, Default is one Minute.</param>
        /// <returns>Response object</returns>
        public object SendRequest<TRequest>(string uri, TRequest obj, int millisecondsTimeout = 60000)
        {
            var data = new TcpBsonRequest<TRequest>
            {
                Id = Guid.NewGuid(),
                URI = uri,
                Object = obj
            };
            TcpBsonRequestContext cxt = new TcpBsonRequestContext(this, data.Id);
            SendPackage(new TcpBsonPackage
            {
                Type = TcpBsonPackageType.Request,
                DataType = typeof(TRequest).AssemblyQualifiedName,
                DataBytes = BsonConvert.SerializeObject(data)
            });
            mWaitResponseContexts.Add(cxt);
            try
            {
                if (cxt.WaitHandler.WaitOne(millisecondsTimeout))
                {
                    if (cxt.IsError)
                    {
                        throw new Exception(cxt.ErrorMessage);
                    }
                    return cxt.Response;
                }
                else
                {
                    throw new TimeoutException("Response is Timeout!");
                }
            }
            finally
            {
                mWaitResponseContexts.Remove(cxt);
            }
        }

#if NETSTANDARD

        /// <summary>
        /// Async Send an Request and wait Response
        /// </summary>
        /// <typeparam name="TRequest">Response Object Type</typeparam>
        /// <param name="uri">A request uri</param>
        /// <param name="obj">Request object</param>
        /// <param name="millisecondsTimeout">Max wait response time</param>
        /// <returns>Response object</returns>
        public Task<object> SendRequestAsync<TRequest>(string uri, TRequest obj, int millisecondsTimeout)
        {
            return Task.Run(() => SendRequest(uri, obj, millisecondsTimeout));
        }
#endif

        /// <summary>
        /// Send a bytes block
        /// </summary>
        /// <param name="bytes">data to send</param>
        /// <param name="callback">Post-Send callback</param>
        /// <exception cref="ArgumentNullException">
        /// <c>bytes</c> is null
        /// </exception>
        public void SendBytes(byte[] bytes, Action callback = null)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            SendPackage(new TcpBsonPackage
            {
                Type = TcpBsonPackageType.Bytes,
                DataBytes = bytes,
                Callback = callback
            });
        }

        private string GetDataType(Type objType)
        {
            return objType.AssemblyQualifiedName;
        }

        /// <summary>
        /// Triggering stoped Events
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();
            lock (mStopHandlers)
            {
                foreach (var handler in mStopHandlers)
                {
                    handler.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Register callback when command is received
        /// </summary>
        /// <param name="callback">callback method</param>
        /// <returns></returns>
        public TcpBsonClient OnReceiveCommand(Action<string> callback)
        {
            lock (mCommandHandlers)
            {
                mCommandHandlers.Add(callback);
            }
            return this;
        }

        /// <summary>
        /// Register callback when a specific type of object is received
        /// </summary>
        /// <typeparam name="T">Type of object received</typeparam>
        /// <param name="callback">callback method</param>
        /// <returns></returns>
        public TcpBsonClient OnReceive<T>(Action<T, TcpBsonClient> callback)
        {
            lock (mJsonHandlers)
            {
                mJsonHandlers.Add(new JsonCallback { Type = typeof(T), Callback = callback });
            }
            return this;
        }

        /// <summary>
        /// Register callback response specific type request
        /// </summary>
        /// <typeparam name="TRequest">The Request type can be handle</typeparam>
        /// <typeparam name="TResponse">The response type callback return</typeparam>
        /// <param name="uri">A request resorce uri</param>
        /// <param name="callback">Invoke when Receive a <c>TRequest</c> request</param>
        /// <returns></returns>
        public TcpBsonClient OnReceiveRequest<TRequest, TResponse>(string uri, Func<TRequest, TcpBsonClient, TResponse> callback)
        {
            lock (mReceiveRequestCallbacks)
            {
                mReceiveRequestCallbacks.Add(new ReceiveRequestCallback(uri, typeof(TRequest), typeof(TResponse), callback));
            }
            return this;
        }

        /// <summary>
        /// Register callback when a bytes block is received
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public TcpBsonClient OnReceiveBytes(Action<byte[], TcpBsonClient> callback)
        {
            lock (mBytesHandlers)
            {
                mBytesHandlers.Add(callback);
            }
            return this;
        }

        /// <summary>
        /// Callback when stopped
        /// </summary>
        /// <param name="callback">callback method</param>
        /// <returns></returns>
        public TcpBsonClient OnStoped(Action<TcpBsonClient> callback)
        {
            lock (mStopHandlers)
            {
                mStopHandlers.Add(callback);
            }
            return this;
        }

        class JsonCallback
        {
            public Type Type { get; set; }
            public Delegate Callback { get; set; }
        }
    }
}
