using System;

namespace Jock.Net.TcpBson
{
    class TcpBsonResponse<T>
    {
        public Guid Id { get; set; }
        public T Object { get; set; }
    }
}
