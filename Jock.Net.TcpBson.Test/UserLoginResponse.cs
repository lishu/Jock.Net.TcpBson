using System;
using System.Collections.Generic;
using System.Text;

namespace Jock.Net.TcpBson.Test
{
    class UserLoginResponse
    {
        public bool Success { get; set; }
        public string ErrMessage { get; set; }
        public string UserName { get; set; }
        public DateTime LoginTime { get; set; }

    }
}
