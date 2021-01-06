
using HamsterCheese.AmongUsMemory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace YourCheese
{
    class Program
    {

        class PlayerInfo
        {
            public uint remainingEmergencies;
            public bool isDead;

            public PlayerInfo(uint re)
            {
                remainingEmergencies = re;
                isDead = false;
            }
        }

        static int tableWidth = 75;

        static List<PlayerData> playerDatas = new List<PlayerData>();
        static AmongUsClient client;
        static ShipStatus ship;
        static String gamecode;

        static int old_meeting_status = 0;
        static uint old_game_state = 0;
        static Dictionary<byte, PlayerInfo> old_player_info = new Dictionary<byte, PlayerInfo>();

        static string path = @"C:\Users\Rahel\source\repos\AmongUsMemory\MyTest.txt";
        static StringBuilder sb = new StringBuilder("");
        static bool inDiscussion = false;
        static bool inGame = false;

        static void Write(String s)
        {
            using (StreamWriter sw = File.AppendText(path))
            {
                Console.Write(s);
                sw.WriteLine(s);
            }
        }

        static void initialize()
        {
            old_player_info.Clear();
            foreach (var data in playerDatas)
            {
                var colorID = data.PlayerInfo.Value.ColorId;
                var re = data.Instance.RemainingEmergencies;

                PlayerInfo pi = new PlayerInfo(re);
                old_player_info[colorID] = pi;
            }
        }

        static string getPlayerWhoCalledMeeting()
        {
            foreach (var data in playerDatas)
            {
                var colorID = data.PlayerInfo.Value.ColorId;
                if (old_player_info[colorID].remainingEmergencies > data.Instance.RemainingEmergencies)
                {
                    old_player_info[colorID].remainingEmergencies = data.Instance.RemainingEmergencies;
                    return Utils.ReadString(data.PlayerInfo.Value.PlayerName);
                }
            }
            return "";
        }


        static void UpdateCheat()
        {
            while (true)
            {
                int meeting_status = Cheese.GetMeetingStatus();
                uint game_state = client.GameState;

                //on start game
                if ((old_game_state == 0 || old_game_state == 1) && game_state == 2)
                {
                    Thread.Sleep(5000); //waiting for imposters to be set and all that jazz.
                    inGame = true;
                    initialize(); 
                    StringBuilder players = new StringBuilder("" + (PlayMap) client.TutorialMapId);
                    StringBuilder imposters = new StringBuilder("imposters");
                    foreach (var data in playerDatas)
                    {
                        var name = Utils.ReadString(data.PlayerInfo.Value.PlayerName);
                        players.Append("," + name);
                        if (data.PlayerInfo.Value.IsImpostor == 1)
                            imposters.Append("," + name);
                    }
                    Write(players.ToString());
                    Write(imposters.ToString());
                }


              
                if (inGame)
                {
                    //check for dead players
                    foreach (var player in playerDatas)
                    {
                        //on player killed
                        if (!old_player_info[player.PlayerInfo.Value.ColorId].isDead && player.PlayerInfo.Value.IsDead == 1)
                        {
                            old_player_info[player.PlayerInfo.Value.ColorId].isDead = true;
                            var victimName = Utils.ReadString(player.PlayerInfo.Value.PlayerName);
                            var killerName = getKiller(player.Position);
                            Write("kill," + killerName + "," + victimName);
                        }
                    }

                    //on discussion start
                    if (old_meeting_status == 0 && meeting_status == 4)
                    {
                        inDiscussion = true;
                        string name = getPlayerWhoCalledMeeting();
                        if (!name.Equals(""))
                        {
                            sb.Append("button," + name);
                        }
                        else //body reported
                        {
                            sb.Append("report");
                        }
                    }

                    //on meeting end
                    if (old_meeting_status == 4 && meeting_status == 0)
                    {
                        Thread.Sleep(5000);
                        foreach (var player in playerDatas)
                        {
                            //on player exiled
                            if (!old_player_info[player.PlayerInfo.Value.ColorId].isDead && player.PlayerInfo.Value.IsDead == 1)
                            {
                                old_player_info[player.PlayerInfo.Value.ColorId].isDead = true;
                                sb.Append("," + Utils.ReadString(player.PlayerInfo.Value.PlayerName));
                                Write(sb.ToString());
                                sb = new StringBuilder();
                            }
                        }
                    }

                    //on game end
                    if (old_game_state == 2 && (game_state == 3 || game_state == 1))
                    {
                        var imposterCount = 0;
                        var crewmateCount = 0;
                        foreach (var data in playerDatas)
                        {
                            if (data.PlayerInfo.Value.IsImpostor == 1)
                            {
                                imposterCount++;
                            }
                            else
                            {
                                crewmateCount++;
                            }
                        }
                        if (imposterCount == crewmateCount)
                        {
                            Write("winner,imposters");
                        }
                        else if (imposterCount == 0)
                        {
                            Write("winner,crewmates");
                        }
                        inGame = false;
                    }
                }
               
                old_game_state = game_state;
                old_meeting_status = meeting_status;

                Thread.Sleep(1000);
            }
        }

       
        static void Main(string[] args)
        {
            // Cheat Init
            if (HamsterCheese.AmongUsMemory.Cheese.Init())
            { 
                // Update Player Data When Every Game
                HamsterCheese.AmongUsMemory.Cheese.ObserveShipStatus((x) =>
                {
                    
                    //stop observe state for init. 
                    foreach(var player in playerDatas) 
                        player.StopObserveState(); 


                    playerDatas = HamsterCheese.AmongUsMemory.Cheese.GetAllPlayers();
                    client = HamsterCheese.AmongUsMemory.Cheese.GetClient();
                    ship = HamsterCheese.AmongUsMemory.Cheese.GetShipStatus();
                    gamecode = HamsterCheese.AmongUsMemory.Cheese.GetGameCode();
                    old_meeting_status = HamsterCheese.AmongUsMemory.Cheese.GetMeetingStatus();
                });

                // Cheat Logic
                CancellationTokenSource cts = new CancellationTokenSource();
                Task.Factory.StartNew(
                    UpdateCheat
                , cts.Token); 
            }

            System.Threading.Thread.Sleep(1000000);
        }

        private static string getKiller(Vector2 pos)
        {
            var killer = "";
            double leastDist = double.MaxValue;
            foreach (var data in playerDatas)
            {
                if (data.PlayerInfo.Value.IsImpostor == 1)
                {
                    double dist = Math.Sqrt(Math.Pow((data.Position.x - pos.x),2) + Math.Pow((data.Position.y - pos.y), 2));

                    if (leastDist > dist)
                    {
                        leastDist = dist;
                        killer = Utils.ReadString(data.PlayerInfo.Value.PlayerName);
                        Console.WriteLine(killer + ": " + data.ReadMemory_KillTimer());
                    }
                }
            }
            return killer;
        }

        static void PrintLine()
        {
            Console.WriteLine(new string('-', tableWidth));
        }

        static void PrintRow(params string[] columns)
        {
            int width = (tableWidth - columns.Length) / columns.Length;
            string row = "|";

            foreach (string column in columns)
            {
                row += AlignCentre(column, width) + "|";
            }

            Console.WriteLine(row);

            
        }

        static string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        } 
    }

    public enum PlayMap
    {
        Skeld = 0,
        Mira = 1,
        Polus = 2
    }
    public enum GameState
    {
        LOBBY,
        TASKS,
        DISCUSSION,
        MENU,
        ENDED,
        UNKNOWN
    }
}


