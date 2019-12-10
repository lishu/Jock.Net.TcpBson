using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Jock.Net.TcpBson.Test
{
    class TestServer
    {
        private static TcpBsonServer mServer;

        internal static void Start(int serverPort)
        {
            mServer = new TcpBsonServer(new IPEndPoint(IPAddress.Any, serverPort));
            mServer.Connecting += MServer_Connecting;
            mServer.Connected += MServer_Connected;
            mServer.Start();
        }

        private static void MServer_Connected(object sender, ConnectedEventArgs e)
        {
            e.ServerClient.OnStoped(OnClientStoped);

            // Set more first ping (Default 5 seconds) stop server fast.
            e.ServerClient.PingTimeSpan = TimeSpan.FromSeconds(1);

            // Cookies all auto sync to client
            e.ServerClient.Cookies["ID"] = Guid.NewGuid().ToString();
            Console.WriteLine($"SERVER Connected, Set Cookies ID is: {e.ServerClient.Cookies["ID"]}");

            // Config request handlers
            e.ServerClient.OnReceiveRequest<UserLoginRequest, UserLoginResponse>("User.Login", OnUserLoginRequest);

            // Just call client session ready, can do more action
            e.ServerClient.SendCommand("Connected");
        }

        private static void OnClientStoped(TcpBsonClient obj)
        {
            Console.WriteLine($"SERVER Client {obj.Cookies["ID"]} Disconnection.");
            if(!mServer.Clients.Any())
            {
                Console.WriteLine($"SERVER All client has gone.");
                Environment.Exit(0);
            }
        }

        private static UserLoginResponse OnUserLoginRequest(UserLoginRequest request, TcpBsonClient client)
        {
            try
            {
                Console.WriteLine($"SERVER Client({client.Cookies["ID"]}) try login by user {request.UserName}");
                // test password only
                if(request.Password != "123456")
                {
                    throw new Exception("The password is incorrect");
                }
                return new UserLoginResponse { Success = true, ErrMessage = "OK", LoginTime = DateTime.Now, UserName = request.UserName };
            }
            catch(Exception e)
            {
                return new UserLoginResponse { Success = false, ErrMessage = e.Message };
            }
        }

        internal static void Stop()
        {
            Console.WriteLine("SERVER Stop");
        }

        private static void MServer_Connecting(object sender, ConnectingEventArgs e)
        {
            // This test server is only for local access
            e.Cancel = !IPAddress.IsLoopback((e.Client.Client.RemoteEndPoint as IPEndPoint).Address);
        }
    }
}
