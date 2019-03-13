using System;
using System.Threading.Tasks;

namespace Artemisa
{
    public static class Artemisa
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Artemisa Server 1.0");
            Server server = new Server();
            if (!server.Startup()) {
                Console.WriteLine("Startup error. Closing server.");
            } else {
                Task.WaitAll(server.Listen());
            }
        }
    }
}
