using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemikSerwer
{
    class main
    {
        public static void Main()
        {
            TCPServer server = new TCPServer();
            server.Run();
        }
    }
}
