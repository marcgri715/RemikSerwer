using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace RemikSerwer
{
    class TCPServer
    {
        static TcpListener listener;
        const int LIMIT = 100;
        private List<Room> listOfRooms;
        private List<PlayerData> playersList;
        private List<PlayerData> serverLobby;
        private int playersCounter;
        private string gameListInfo;


        public TCPServer()
        {
            listOfRooms = new List<Room>();
            playersList = new List<PlayerData>();
            serverLobby = new List<PlayerData>();
            try
            {
                listener = new TcpListener(IPAddress.Any, 13131);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not create listener: {0}", ex.Message);
                Environment.Exit(1);
            }
            Console.WriteLine("Server successfully started!");
            Console.WriteLine();
        }

        public void Run() 
        {
            listener.Start();
            while (true)
            {
                TcpClient newClient = listener.AcceptTcpClient();
                if (playersList.Count < LIMIT)
                {
                    PlayerData player = new PlayerData(newClient);
                    playersList.Add(player);
                    serverLobby.Add(player);
                    Thread playerThread = new Thread(Service);
                    playerThread.Start(player);
                    //wyślij że połączono
                    //pobierz nazwę użytkownika
                    //wyślij początkowe info o pokojach
                }
                else
                {
                    //wyślij że brakło miejsca
                }
                foreach (PlayerData player in playersList)
                {
                    if (!player.GetClient().Connected)
                    {
                        if (player.currentRoom == -1)
                        {
                            serverLobby.Remove(player);
                        }
                        else
                        {
                            listOfRooms.Find(x => x.getId() == player.currentRoom).playerLeavingRoom(player);
                        }
                        playersList.Remove(player);
                        Console.WriteLine("User {0} disconnected!", player.login);
                    }
                }
            }
        }

        private void Service(object pPlayer)
        {
            PlayerData player = (PlayerData)pPlayer;
            TcpClient client = player.GetClient();
            player.address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            Console.WriteLine("New player connected from address {0} ({1})!", player.address, Dns.GetHostEntry(player.address).HostName);
            Console.WriteLine("Number of players on server: {0}", playersList.Count);
            while (client.Connected)
            {
                byte[] buffer = new byte[1024];
                int received = 0;
                try
                {
                    received = client.GetStream().Read(buffer, 0, 1024);
                }
                catch (Exception ex)
                {
                }

                if (!client.Connected)
                    break;

                string fromClient = Encoding.UTF8.GetString(buffer, 0, received);
                Console.WriteLine("[{0} ({1})] {2}", player.login, player.address, fromClient);
                DecipherString(fromClient, player);
                Thread.Sleep(50);
            }

            Console.WriteLine("Client {0} ({1}) has disconnected.", player.login, player.address);

            //wyślij że rozłączono
        }

        private void DecipherString(string message, PlayerData player)
        {
            string[] messageParts = message.Split('|');
            int index;
            switch (messageParts[0])
            {
                case "LOGIN":
                    if (!playersList.Any(x => x.login == messageParts[1]))
                    {
                        player.login = messageParts[1];
                        Console.WriteLine("Player from address {0} set his name to {1}.", player.address, player.login);
                        SendMessageToPlayer(player, "LOGIN", "accepted");
                    }
                    else
                    {
                        SendMessageToPlayer(player, "LOGIN", "rejected");
                    }
                    break;
                case "ROOMLIST":
                    string response = listOfRooms.Count.ToString() + "|";
                    foreach (Room room in listOfRooms)
                    {
                        response += room.getRoomInfo();
                    }
                    SendMessageToPlayer(player, "ROOMLIST", response);
                    break;
                case "CREATE":
                    try
                    {
                        int id = 0;
                        bool idIsFree;
                        do
                        {
                            idIsFree = true;
                            foreach (Room room in listOfRooms)
                            {
                                if (id == room.getId())
                                {
                                    idIsFree = false;
                                    id++;
                                    break;
                                }
                            }
                        } while (!idIsFree);
                        Room newRoom = new Room(player,id);
                        listOfRooms.Add(newRoom);
                        foreach (PlayerData plr in playersList)
                        {
                            if (plr.currentRoom == -1)
                            {
                                SendMessageToPlayer(plr, "NEWROOM", newRoom.getRoomInfo());
                            }
                        }
                        SendMessageToPlayer(player, "CREATE", "SUCCESS");
                        Console.WriteLine("New room created by {0} (room id:{1})",player.login, newRoom.getId());
                        }
                    catch (Exception ex)
                    {
                        SendMessageToPlayer(player, "CREATE", "FAILED");
                        Console.WriteLine("Failed to create new room! (Requested by {0})", player.login);
                    }
                    break;
                case "JOIN":
                    JoinRoom(player, int.Parse(messageParts[1]), false);
                    break;
                case "INVITED":
                    JoinRoom(player, int.Parse(messageParts[1]), true);
                    break;
                case "CHAT":
                    index = int.Parse(messageParts[1]);
                    SendMessageToRoom(listOfRooms.Find(x => x.getId() == index), "CHAT", messageParts[2]);
                    break;
            }
        }



        private bool JoinRoom(PlayerData player, int index, bool isInvited)
        {
            Room selected = listOfRooms.Find(x => x.getId() == index);
            if (!selected.privateFlag || isInvited)
            {
                player.currentRoom = index;
                selected.addPlayerToRoom(player);
                SendMessageToPlayer(player, "JOIN", "ACCEPTED");
                Console.WriteLine("Player {0} joined room {1}.", player.login, selected.getId());
                foreach (PlayerData plr in selected.getLobby())
                {
                    SendMessageToPlayer(plr, "JOINED", player.login + " has joined the room.");
                }
                serverLobby.Remove(player);
                return true;
            }
            else
            {
                SendMessageToPlayer(player, "JOIN", "DECLINED");
                return false;
            }
        }

        private void SendMessageToPlayer(PlayerData player, string command, string message)
        {
            string toSend = command + "|" + message;
            if (!player.GetClient().Connected)
                return;
            try
            {
                player.GetClient().Client.Send(Encoding.UTF8.GetBytes(toSend), SocketFlags.None);
            }
            catch (Exception ex)
            {
            }
        }

        private void SendMessageToAllPlayers(string command, string message)
        {
            string toSend = command + "|" + message;
            foreach (PlayerData player in playersList)
            {
                if (!player.GetClient().Connected)
                    return;
                try
                {
                    player.GetClient().Client.Send(Encoding.UTF8.GetBytes(toSend), SocketFlags.None);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void SendMessageToRoom(Room room, string command, string message)
        {
            string toSend = command + "|" + message;
            foreach (PlayerData player in room.getLobby())
            {
                if (!player.GetClient().Connected)
                    return;
                try
                {
                    player.GetClient().Client.Send(Encoding.UTF8.GetBytes(toSend), SocketFlags.None);
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}
