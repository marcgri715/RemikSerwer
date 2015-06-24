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
        public bool privateFlag;

        public Room(PlayerData creator, int pId)
        {
            lobby = new List<PlayerData>();
            lobby.Add(creator);
            timeLimit = 0;
            playersMax = 2;
            privateFlag = false;
            slots = new PlayerData[2];
            creator.currentRoom = id = pId;
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
            if (privateFlag)
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
    }
}
