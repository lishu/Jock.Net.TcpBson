using System;
using System.Threading;

namespace Jock.Net.TcpBson
{
    class TcpBsonRequestContext
    {
        public TcpBsonRequestContext(TcpBsonClient TcpBsonClient, Guid id)
        {
            tCreateTime = DateTime.Now;
            Client = TcpBsonClient;
            Id = id;
            WaitHandler = new AutoResetEvent(false);
        }

        private DateTime tCreateTime;

        public TcpBsonClient Client { get; private set; }
        public Guid Id { get; private set; }
        public AutoResetEvent WaitHandler { get; }
        public object Response { get; private set; }
        public bool IsError { get; private set; }
        public string ErrorMessage { get; private set; }

        public void SetResponse(object response)
        {
            Response = response;
            WaitHandler.Set();
        }

        public void SetError(string error)
        {
            IsError = true;
            ErrorMessage = error;
            WaitHandler.Set();
        }
    }
}
