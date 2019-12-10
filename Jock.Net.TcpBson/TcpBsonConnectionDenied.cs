using System;

namespace Jock.Net.TcpBson
{
    /// <summary>
    /// EventArgs for TcpBsonClient connection denied
    /// </summary>
    public class TcpBsonConnectionDeniedEventArgs : EventArgs
    {
        internal TcpBsonConnectionDeniedEventArgs(TcpBsonClient client)
        {
            this.Client = client;
        }

        /// <summary>
        /// TcpBsonClient instance
        /// </summary>
        public TcpBsonClient Client { get; }

        /// <summary>
        /// set True if want try reconnection
        /// </summary>
        public bool Retry { get; set; }
    }

    /// <summary>
    /// Delegate for TcpBsonClient connection denied
    /// </summary>
    /// <param name="e"></param>
    public delegate void TcpBsonConnectionDeniedEventHandler(TcpBsonConnectionDeniedEventArgs e);
}
