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
        private int index;
        private Room foundRoom;


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
                PlayerData newPlayer = new PlayerData(newClient);
                if (playersList.Count < LIMIT)
                {
                    playersList.Add(newPlayer);
                    serverLobby.Add(newPlayer);
                    Thread playerThread = new Thread(Service);
                    playerThread.Start(newPlayer);
                    SendMessageToPlayer(newPlayer, "CONNECT", "SUCCESS");
                    //wyślij że połączono
                    //pobierz nazwę użytkownika
                }
                else
                {
                    //wyślij że brakło miejsca
                    SendMessageToPlayer(newPlayer, "CONNECT", "FULL");
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
                catch (Exception)
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
            string response;
            int slot;
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
                    response = listOfRooms.Count.ToString() + "|";
                    foreach (Room room in listOfRooms)
                    {
                        response += room.getRoomInfo();
                    }
                    SendMessageToPlayer(player, "ROOMLIST", response);
                    Console.WriteLine("Sent roomlist to player {0} [{1}].", player.login, player.address);
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
                        SendMessageToGroup(serverLobby, "NEWROOM", newRoom.getRoomInfo());
                        SendMessageToPlayer(player, "CREATE", "SUCCESS");
                        Console.WriteLine("New room created by {0} (room id:{1})",player.login, newRoom.getId());
                        }
                    catch (Exception)
                    {
                        SendMessageToPlayer(player, "CREATE", "FAILED");
                        Console.WriteLine("Failed to create new room! (Requested by {0})", player.login);
                    }
                    break;
                case "JOIN":
                    SelectRoom(messageParts[1]);
                    if (JoinRoom(player))
                    {
                        Console.WriteLine("{0} has joined room {1}.", player.login, index);
                    }
                    else
                    {
                        Console.WriteLine("{0} has failed to join room {1}.", player.login, index);
                    }
                    break;
                case "PASSWORD":
                    SelectRoom(messageParts[1]);
                    if (JoinRoom(player, messageParts[2]))
                    {
                        Console.WriteLine("{0} has joined room {1}.", player.login, index);
                    }
                    else
                    {
                        Console.WriteLine("{0} has failed to join room {1}.", player.login, index);
                    }
                    break;
                case "CHAT":
                    SelectRoom(messageParts[1]);
                    SendMessageToGroup(foundRoom.getLobby(), "CHAT", messageParts[1] + "|" + messageParts[2]);
                    Console.WriteLine("[Room {0}] {1}: {2}", index, player.login, messageParts[2]);
                    break;
                case "LEAVE":
                    SelectRoom(messageParts[1]);
                    foundRoom.playerLeavingRoom(player);
                    if (foundRoom.getLobby().Count == 0)
                    {
                        listOfRooms.Remove(foundRoom);
                        SendMessageToGroup(serverLobby, "ROOMREMOVED",foundRoom.getId().ToString());
                    }
                    serverLobby.Add(player);
                    Console.WriteLine("Player {0} left room {1}.", player.login, index);
                    break;
                case "TAKESLOT":
                    SelectRoom(messageParts[1]);
                    slot = int.Parse(messageParts[2]);
                    if (foundRoom.putPlayerInSlot(player, slot))
                    {
                        SendMessageToGroup(foundRoom.getLobby(), "SLOTTAKEN", messageParts[1] + "|" + slot.ToString() + "|" + player.login, true);
                        Console.WriteLine("Player {0} took slot {1} in room {2}.", player.login, slot, index);
                    }
                    else
                    {
                        SendMessageToPlayer(player, "TAKESLOT", "FAILED");
                    }
                    break;
                case "FREESLOT":
                    SelectRoom(messageParts[1]);
                    slot = int.Parse(messageParts[2]);
                    foundRoom.playerLeavingSlot(slot);
                    SendMessageToGroup(foundRoom.getLobby(), "SLOTFREED", slot.ToString(), true);
                    Console.WriteLine("Player {0} left slot {1} in room {2}.", player.login, slot, index);
                    break;
                case "SETTINGS":
                    switch (messageParts[1])
                    {
                        case "PLAYERS": 
                            SelectRoom(messageParts[2]);
                            int maxPlayers = int.Parse(messageParts[3]);
                            foundRoom.setMaxPlayers(maxPlayers);
                            SendMessageToGroup(foundRoom.getLobby(), "PLAYERS", messageParts[2] + "|" + messageParts[3], true);
                            Console.WriteLine("Room {0}: max players changed to {1}", index, maxPlayers);
                            break;
                        case "TIME":
                            SelectRoom(messageParts[2]);
                            int maxTime = int.Parse(messageParts[3]);
                            foundRoom.setTime(maxTime);
                            SendMessageToGroup(foundRoom.getLobby(), "TIME", messageParts[2] + "|" + messageParts[3], true);
                            Console.WriteLine("Room {0}: max time changed to {1}", index, maxTime);
                            break;
                        case "SCORES":
                            SelectRoom(messageParts[2]);
                            slot = int.Parse(messageParts[3]);
                            int newScore = int.Parse(messageParts[4]);
                            foundRoom.setScore(slot, newScore);
                            SendMessageToGroup(foundRoom.getLobby(), "SCORE", messageParts[3] + "|" + messageParts[4]);
                            Console.WriteLine("Room {0}: score in slot {1} changed to {2}", index, slot, newScore);
                            break;
                        case "PASSWORD":
                            SelectRoom(messageParts[2]);
                            string newPassword = messageParts[3];
                            if (foundRoom.setPassword(newPassword))
                            {
                                response = "SET";
                            }
                            else
                            {
                                response = "CLEAR";
                            }
                            SendMessageToGroup(foundRoom.getLobby(), "PASSWORD", response, true);
                            break;
                    }
                    break;
                case "KICK":
                    SelectRoom(messageParts[1]);
                    string toBeKicked = messageParts[2];
                    foreach (PlayerData kicked in foundRoom.getLobby())
                    {
                        if (kicked.login == toBeKicked)
                        {
                            foundRoom.getLobby().Remove(kicked);
                            serverLobby.Add(kicked);
                            SendMessageToPlayer(kicked, "KICKED", "");
                            SendMessageToGroup(foundRoom.getLobby(), "KICK", toBeKicked);
                            break;
                        }
                    }
                    break;
                case "READY":
                    SelectRoom(messageParts[1]);
                    slot = int.Parse(messageParts[2]);
                    if (foundRoom.switchReady(slot))
                    {
                        Random start = new Random();
                        int startingPlayer = start.Next(foundRoom.GetMaxPlayers());
                        SendMessageToGroup(foundRoom.getLobby(), "START", startingPlayer.ToString());
                    } 
                    else 
                    {
                        SendMessageToGroup(foundRoom.getLobby(), "READY", messageParts[2]);
                    }
                    break;
                    //todo: game controls
                default:
                    Console.WriteLine("Unknown client command: \"{0}\" by {1} [{2}]", message, player.login, player.address);
                    break;
            }
        }



        private bool JoinRoom(PlayerData player, string password = null)
        {
            bool allowed = true;
            if (foundRoom.hasPassword)
            {
                allowed = foundRoom.checkPassword(password);
            }
            if (allowed)
            {
                player.currentRoom = index;
                foundRoom.addPlayerToRoom(player);
                SendMessageToPlayer(player, "JOIN", "ACCEPTED");
                Console.WriteLine("Player {0} joined room {1}.", player.login, foundRoom.getId());
                foreach (PlayerData plr in foundRoom.getLobby())
                {
                    SendMessageToPlayer(plr, "JOINED", player.login + " has joined the room.");
                }
                serverLobby.Remove(player);
                return true;
            }
            SendMessageToPlayer(player, "JOIN", "DECLINED");
            return false;
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
            catch (Exception)
            {
            }
        }

        private void SendMessageToAllPlayers(string command, string message)
        {
            foreach (PlayerData player in playersList)
            {
                SendMessageToPlayer(player, command, message);
            }
        }

        private void SendMessageToGroup(List<PlayerData> lobby, string command,  string message, bool sendToLobby=false)
        {
            string toSend = command + "|" + message;
            foreach (PlayerData player in lobby)
            {
                SendMessageToPlayer(player, command, message);
            }
            if (sendToLobby)
            {
                foreach (PlayerData player in serverLobby)
                {
                    SendMessageToPlayer(player, command, message);
                }
            }
        }

        private void SelectRoom(string part)
        {
            index = int.Parse(part);
            foundRoom = listOfRooms.Find(x => x.getId() == index);
        }
    }
}
