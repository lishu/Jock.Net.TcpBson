using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Jock.Net.TcpBson
{
    /// <summary>
    /// Hold a collection of name string cookies, Auto sync between tow connected <c>TcpBsonClient</c>.
    /// </summary>
    public class TcpBsonCookies : IDictionary<string, string>, ISupportInitialize
    {
        private TcpBsonClient mClient;
        private bool mInInitMode;
        private Dictionary<string, string> mCookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<TcpBsonCookieSync> mInitSyncs = new List<TcpBsonCookieSync>();

        internal TcpBsonCookies(TcpBsonClient client)
        {
            mClient = client;
        }

        /// <summary>
        /// Get or set cookie by spec key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                if (mCookies.TryGetValue(key, out string value))
                {
                    return value;
                }
                return null;
            }
            set
            {
                lock (this)
                {
                    if (value == null)
                    {
                        if (mCookies.Remove(key))
                        {
                            SendSync(new TcpBsonCookieSync { Action = TcpBsonCookieSyncAction.Remove, Name = key });
                        }
                        return;
                    }

                    var isUpdate = mCookies.ContainsKey(key);
                    mCookies[key] = value;
                    SendSync(new TcpBsonCookieSync { Action = isUpdate ? TcpBsonCookieSyncAction.Update : TcpBsonCookieSyncAction.Add, Name = key, Value = value });
                }
            }
        }

        /// <summary>
        /// Gets a collection containing the keys in the <c>TcpBsonCookies</c>
        /// </summary>
        public ICollection<string> Keys => mCookies.Keys;

        /// <summary>
        /// Gets a collection containing the values in the <c>TcpBsonCookies</c>
        /// </summary>
        public ICollection<string> Values => mCookies.Values;

        /// <summary>
        /// Gets this number of cookies contained in the <c>TcpBsonCookies</c>
        /// </summary>
        public int Count => mCookies.Count;

        /// <summary>
        /// Cookies is read only, allow return <c>false</c>.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Add a cookie to the cookies.
        /// </summary>
        /// <param name="key">Cookie name</param>
        /// <param name="value">Cookie value</param>
        public void Add(string key, string value)
        {
            mCookies.Add(key, value);
            SendSync(new TcpBsonCookieSync { Action = TcpBsonCookieSyncAction.Add, Name = key, Value = value });
        }

        /// <summary>
        /// Add a cookie to the cookies
        /// </summary>
        /// <param name="item">A <c>KeyValuePair&lt;string, string&gt;</c> has Key and Value</param>
        public void Add(KeyValuePair<string, string> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// Begin a init, This will pause cookie sync.
        /// </summary>
        public void BeginInit()
        {
            mInInitMode = true;
        }

        /// <summary>
        /// Clear all cookies
        /// </summary>
        public void Clear()
        {
            Values.Clear();
            if (mInInitMode)
            {
                lock (mInitSyncs)
                {
                    mInitSyncs.Clear();
                }
            }
            SendSync(new TcpBsonCookieSync
            {
                Action = TcpBsonCookieSyncAction.Clear
            });
        }

        private void SendSync(TcpBsonCookieSync sync)
        {
            if (mInInitMode)
            {
                lock (mInitSyncs)
                {
                    mInitSyncs.Add(sync);
                }
            }
            else
            {
                mClient.SendPackage(new TcpBsonPackage { Type = TcpBsonPackageType.CookieSync, DataType = typeof(TcpBsonCookieSync).AssemblyQualifiedName, DataBytes = BsonConvert.SerializeObject(sync) });
            }
        }

        /// <summary>
        /// Determines whether the <c>TcpBsonCookies</c>contains the specified key and equal value.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<string, string> item)
        {
            return ContainsKey(item.Key) && this[item.Key] == item.Value;
        }

        /// <summary>
        /// Determines whether the <c>TcpBsonCookies</c>contains the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <c>TcpBsonCookies</c></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            return mCookies.ContainsKey(key);
        }

        /// <summary>
        /// Copy all item to array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            foreach (var kv in this)
            {
                array[arrayIndex++] = new KeyValuePair<string, string>(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// End a init, This will restore cookie sync.
        /// </summary>
        public void EndInit()
        {
            mInInitMode = false;
            if (mInitSyncs.Any())
            {
                lock (mInitSyncs)
                {
                    foreach (var sync in mInitSyncs)
                    {
                        mClient.SendPackage(new TcpBsonPackage { Type = TcpBsonPackageType.CookieSync, DataType = typeof(TcpBsonCookieSync).AssemblyQualifiedName, DataBytes = BsonConvert.SerializeObject(sync) });
                    }
                    mInitSyncs.Clear();
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <c>TcpBsonCookies</c>
        /// </summary>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return mCookies.GetEnumerator();
        }

        /// <summary>
        /// Removes the value with the specified key from the <c>TcpBsonCookies</c>
        /// </summary>
        public bool Remove(string key)
        {
            if (mCookies.ContainsKey(key))
            {
                lock (this)
                {
                    if (mCookies.ContainsKey(key))
                    {
                        mCookies.Remove(key);
                        SendSync(new TcpBsonCookieSync { Action = TcpBsonCookieSyncAction.Remove, Name = key });
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Removes the value with the specified key from the <c>TcpBsonCookies</c>
        /// </summary>
        public bool Remove(KeyValuePair<string, string> item)
        {
            return Remove(item.Key);
        }

        /// <summary>
        /// Try Get Value with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(string key, out string value)
        {
            return mCookies.TryGetValue(key, out value);
        }

        internal void Sync(TcpBsonPackage package)
        {
            var sync = BsonConvert.DeserializeObject<TcpBsonCookieSync>(package.DataBytes);
            lock (this)
            {
                switch (sync.Action)
                {
                    case TcpBsonCookieSyncAction.Clear:
                        mCookies.Clear();
                        break;
                    case TcpBsonCookieSyncAction.Add:
                    case TcpBsonCookieSyncAction.Update:
                        mCookies[sync.Name] = sync.Value;
                        break;
                    case TcpBsonCookieSyncAction.Remove:
                        mCookies.Remove(sync.Name);
                        break;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
