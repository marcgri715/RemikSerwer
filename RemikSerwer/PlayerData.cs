using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace RemikSerwer
{
    class PlayerData
    {
        private readonly TcpClient client;
        public string login { get; set; }
        public int currentRoom { get; set; }
        public IPAddress address { get; set; }

        public PlayerData(TcpClient pClient)
        {
            currentRoom = -1;
            client = pClient;
            login = "";
        }

        public TcpClient GetClient()
        {
            return client;
        }
       
    }
}
