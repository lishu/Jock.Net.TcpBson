using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Jock.Net.TcpBson.Test
{
    class TestClient
    {
        private static TcpBsonClient mClient;

        internal static void Start(int serverPort)
        {
            mClient = new TcpBsonClient(new IPEndPoint(IPAddress.Loopback, serverPort));
            mClient.OnReceiveCommand(OnReceiveCommand);
            // DO FORGOT START IT
            mClient.Start();
        }

        private static void OnReceiveCommand(string obj)
        {
            Console.WriteLine($"CLIENT: OnReceiveCommand {obj}");
            switch (obj)
            {
                case "Connected":
                    Console.WriteLine($"CLIENT: COOKEID is {mClient.Cookies["ID"]}");
                    TryLogin();
                    break;
            }
        }

        // This invoke in OnReceiveCommand is in core loop, Do not user sync version request, It stop client receive loop
        private static async void TryLogin()
        {
            try
            {
                var resp = await mClient.SendRequestAsync("User.Login", new UserLoginRequest { UserName = "Jock", Password = "123456" }, 5000);
                if (resp is UserLoginResponse)
                {
                    var lr = resp as UserLoginResponse;
                    Console.WriteLine($"CLIENT Login Response: {lr.ErrMessage}");
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("CLIENT Login Error: " + e.Message);
            }
            finally
            {
                mClient.Stop();
            }
        }

        internal static void Stop()
        {
            mClient.Stop();
        }
    }
}
