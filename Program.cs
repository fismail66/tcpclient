using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tcpclient
{
    class Program
    {
        public static CancellationTokenSource cancellationSource;
        static void Main(string[] args)
        {
            try
            {
                
                cancellationSource = new CancellationTokenSource();
                RemoteServerInfo rsi = new RemoteServerInfo
                {
                    Host = IPAddress.Parse("127.0.0.1"),
                    Port = 900
                };
                AsyncTcpClient client = new AsyncTcpClient();
                //client.OnDataReceived += HandleRecieved;
                client.OnDataReceived = onData;
                client.OnDisconnected = onDisconnect;
                client.OnException = OnException;
                client.OnException += OnExceptionStack;
                //client.OnDisconnected += HandleDisconnected;
                client.ConnectAsync(rsi, cancellationSource.Token).ContinueWith(t => client
                        .Recieve(cancellationSource.Token), 
                        TaskContinuationOptions.OnlyOnRanToCompletion).Wait();
                Console.WriteLine("here we are");
            }
            catch (System.Exception ex)
            {
                Console.Write(ex.Message);
            }


            Console.Read();
        }

        static void OnException(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        static void OnExceptionStack(Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
        }
        static void onData(byte[] data)
        {
            Console.WriteLine(Encoding.ASCII.GetString(data));
        }
        static void onDisconnect()
        {
            cancellationSource.Cancel();
        }
        static void HandleRecieved(object client, byte[] data)
        {
            Console.WriteLine(Encoding.ASCII.GetString(data));
        }

        static void HandleDisconnected(object client, EventArgs e)
        {

            cancellationSource.Cancel();
        }
    }
}
