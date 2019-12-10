using System;

namespace Jock.Net.TcpBson.Test
{
    class Program
    {
        const int ServerPort = 8123;
        static void Main(string[] args)
        {
            // Start a TestServer
            TestServer.Start(ServerPort);

            // Start a TestClient
            // You can create more client but for test is only one instaince
            TestClient.Start(ServerPort);

            Console.CancelKeyPress += Console_CancelKeyPress;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            TestClient.Stop();
            TestServer.Stop();
        }
    }
}
