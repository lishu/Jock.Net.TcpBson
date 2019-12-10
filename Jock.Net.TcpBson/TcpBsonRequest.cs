using System;

namespace Jock.Net.TcpBson
{
    class TcpBsonRequest<T> : TcpBsonRequest
    {
        public new T Object
        {
            get
            {
                return (T)base.Object;
            }
            set
            {
                base.Object = value;
            }
        }
    }

    class TcpBsonRequest
    {
        public Guid Id { get; set; }
        public string URI { get; set; }
        public object Object { get; set; }
    }
}
