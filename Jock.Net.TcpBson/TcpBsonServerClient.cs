using System.Net.Sockets;

namespace Jock.Net.TcpBson
{
    /// <summary>
    /// The communication side of the Bson server
    /// </summary>
    public class TcpBsonServerClient : TcpBsonClient
    {
        internal TcpBsonServerClient(TcpBsonServer server, TcpClient tcpClient) : base(tcpClient)
        {
            Server = server;
        }

        /// <summary>
        /// Get the relevant service side <c>TcpBsonServer</c>
        /// </summary>
        public TcpBsonServer Server { get; }
    }
}
