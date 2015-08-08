using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemikSerwer
{
    class Room
    {
        private List<PlayerData> lobby;
        private PlayerData[] slots;
        private int timeLimit;
        private int playersMax;
        private int id;
        private string password;
        public bool hasPassword;
        private PlayerData host;
        private bool[] slotReadiness;
        /*TODO logika gry*/

        public Room(PlayerData creator, int pId)
        {
            lobby = new List<PlayerData>();
            host = creator;
            lobby.Add(creator);
            timeLimit = 0;
            playersMax = 2;
            hasPassword = false;
            slots = new PlayerData[2];
            slotReadiness = new bool[2];
            slotReadiness[0] = slotReadiness[1] = false;
            creator.currentRoom = id = pId;
            password = "";
        }

        public string getRoomInfo()
        {
            string info = id.ToString() + "|";
            info += timeLimit.ToString();
            info += '|';
            info += playersMax.ToString();
            info += '|';
            foreach (PlayerData player in slots) {
                if (player != null)
                {
                    info += player.login;
                }
                else
                {
                    info += "-";
                }
                info += '|';
            }
            if (hasPassword)
            {
                info += "1";
            }
            else
            {
                info += "0";
            }
            return info;
        }

        public void addPlayerToRoom(PlayerData newPlayer)
        {
            lobby.Add(newPlayer);
        }

        public void playerLeavingRoom(PlayerData leavingPlayer)
        {
            int index = Array.FindIndex(slots, player => player == leavingPlayer);
            if (index >= 0)
            {
                slots[index] = null;
            }
            lobby.Remove(leavingPlayer);
            if (leavingPlayer == host)
            {
                if (lobby.Count > 0)
                {
                    host = null;
                    foreach (PlayerData newHost in slots)
                    {
                        if (newHost != null)
                        {
                            host = newHost;
                        }
                    }
                    if (host == null)
                    {
                        host = lobby[0];
                    }
                }
            }
        }

        public bool putPlayerInSlot(PlayerData player, int index)
        {
            if (slots[index] == null)
            {
                slots[index] = player;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void playerLeavingSlot(int index)
        {
            slots[index] = null;
        }

        public int getId()
        {
            return id;
        }

        public List<PlayerData> getLobby()
        {
            return lobby;
        }

        public bool checkPassword(string pwd)
        {
            return (password == pwd);
        }

        public void setMaxPlayers(int newValue)
        {
            PlayerData[] newSlots = new PlayerData[newValue];
            int limit = newValue < playersMax ? newValue : playersMax;
            for (int i = 0; i < limit; i++)
            {
                newSlots[i] = slots[i];
            }
            slots = newSlots;
            playersMax = newValue;
            slotReadiness = new bool[playersMax];
            for (int i = 0; i < playersMax; i++)
            {
                slotReadiness[i] = false;
            }
        }

        public void setTime(int newValue)
        {
            timeLimit = newValue;
        }

        public void setScore(int slot, int newValue)
        {
            //TODO: set score after creating logic
        }

        public bool setPassword(string newPassword)
        {
            password = newPassword;
            if (newPassword.Length == 0)
            {
                hasPassword = false;
                return false;
            }
            hasPassword = true;
            return true;
        }

        public bool switchReady(int slot)
        {
            slotReadiness[slot] = !slotReadiness[slot];
            for (int i = 0; i < slotReadiness.Length; i++)
            {
                if (slotReadiness[i]==false)
                {
                    return false;
                }
            }
            return true;
        }

        public int GetMaxPlayers()
        {
            return playersMax;
        }
    }
}
