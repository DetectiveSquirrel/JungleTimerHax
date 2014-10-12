using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace JungleTimerHax
{
    internal class Program
    {
        public static Menu Config;
        public const String BaseUrl = "http://www.lolnexus.com/ajax/get-game-info/";
        public const String UrlPartial = ".json?name=";
        public const String SearchString = "lrf://spectator ";

        public static Dictionary<String, UInt32> jungleIds = new Dictionary<String, UInt32>();
        public static Dictionary<String, Vector3> junglePos = new Dictionary<String, Vector3>();
        public static Dictionary<String, Vector3> junglePos2 = new Dictionary<String, Vector3>();
        public static Dictionary<String, Single> jungleRespawns = new Dictionary<String, Single>();
        public static String Key;
        public static String GameId;
        public static String PlatformId;
        public static String RegionTag;
        public static String SpecUrl;
        public static Single TimeOffset = 0;
        private static void Main(string[] args)
        {
            GetRegionInfo();
            GetSpecInfo();
            Config = new Menu("JungleTimerHax", "JungleTimerHax", true);
            Config.AddToMainMenu();
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnGameProcessPacket += Game_OnGameProcessPacket;           
        }
        static void Game_OnGameProcessPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] == 0xC1 || args.PacketData[0] == 0xC2)
            {
                TimeOffset = Game.Time - BitConverter.ToSingle(args.PacketData, 5);
                new System.Threading.Thread(() =>
                {
                    GetTimers();
                }).Start();
            }
        }
        static void GetRegionInfo()
        {
            Process proc = Process.GetProcesses().First(p => p.ProcessName.Contains("League of Legends"));
            String propFile = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(proc.Modules[0].FileName))))));
            propFile += @"\projects\lol_air_client\releases\";
            DirectoryInfo di = new DirectoryInfo(propFile).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc).First();
            propFile = di.FullName + @"\deploy\lol.properties";
            propFile = File.ReadAllText(propFile);
            SpecUrl = new Regex("featuredGamesURL=(.+)featured").Match(propFile).Groups[1].Value;
            RegionTag = new Regex("regionTag=(.+)\r").Match(propFile).Groups[1].Value;
            SpectatorService.SpectatorDownloader.specHtml = SpecUrl;
            Game.PrintChat(SpecUrl);
        }
        static void GetSpecInfo()
        {
            String GameInfo = new WebClient().DownloadString(BaseUrl + RegionTag + UrlPartial + ObjectManager.Player.Name);
            GameInfo = GameInfo.Substring(GameInfo.IndexOf(SearchString) + SearchString.Length);
            GameInfo = GameInfo.Substring(GameInfo.IndexOf(" ") + 1);
            Key = GameInfo.Substring(0, GameInfo.IndexOf(" "));
            GameInfo = GameInfo.Substring(GameInfo.IndexOf(" ") + 1);
            GameId = GameInfo.Substring(0, GameInfo.IndexOf(" "));
            GameInfo = GameInfo.Substring(GameInfo.IndexOf(" ") + 1);
            PlatformId = GameInfo.Substring(0, GameInfo.IndexOf(" "));
        }
        static void GetTimers()
        {
            List<Packets.Packet> packets = new List<Packets.Packet>();
            List<Byte[]> fullGameBytes = SpectatorService.SpectatorDownloader.DownloadGameFiles(GameId, PlatformId, Key, "Chunk");
            foreach (Byte[] chunkBytes in fullGameBytes)
            {
                packets.AddRange(SpectatorService.SpectatorDecoder.DecodeBytes(chunkBytes));
            }
            foreach (Packets.Packet p in packets)
            {
                if (p.header == (Byte)Packets.HeaderList.JungleSpawn)
                {
                    Packets.JungleSpawn jungleSpawn = new Packets.JungleSpawn(p);
                    jungleIds[jungleSpawn.creepName] = jungleSpawn.netId;
                    junglePos2[jungleSpawn.creepName] = new Vector3(jungleSpawn.x, jungleSpawn.y, jungleSpawn.z);
                }
                else if (p.header == (Byte)Packets.HeaderList.ExperienceGain)
                {
                    Packets.ExperienceGain xpPacket = new Packets.ExperienceGain(p);
                    jungleIds.Keys.ToList().ForEach(delegate(String jungleCreep)
                    {
                        if (xpPacket.GivingNetId == jungleIds[jungleCreep] && jungleIds[jungleCreep] != 0)
                        {
                            jungleIds[jungleCreep] = 0;
                            String camp = System.Text.RegularExpressions.Regex.Match(jungleCreep, @"\d+").ToString();
                            bool allDead = true;
                            jungleIds.Keys.ToList().ForEach(delegate(String creep)
                            {
                                String camp2 = System.Text.RegularExpressions.Regex.Match(creep, @"\d+").ToString();
                                if (camp2 == camp && jungleIds[creep] > 0)
                                    allDead = false;
                            });
                            if (allDead)
                            {
                                if (camp.Equals("1"))
                                {
                                    jungleRespawns["LeftBlue"] = xpPacket.time + 300;
                                    junglePos["LeftBlue"] = junglePos2[jungleCreep];
                                }
                                else if (camp.Equals("4"))
                                {
                                    jungleRespawns["BotRed"] = xpPacket.time + 300;
                                    junglePos["BotRed"] = junglePos2[jungleCreep];
                                }
                                else if (camp.Equals("7"))
                                {
                                    jungleRespawns["RightBlue"] = xpPacket.time + 300;
                                    junglePos["RightBlue"] = junglePos2[jungleCreep];
                                }
                                else if (camp.Equals("10"))
                                {
                                    jungleRespawns["TopRed"] = xpPacket.time + 300;
                                    junglePos["TopRed"] = junglePos2[jungleCreep];
                                }
                                else if (camp.Equals("6"))
                                {
                                    jungleRespawns["Dragon"] = xpPacket.time + 360;
                                    junglePos["Dragon"] = junglePos2[jungleCreep];
                                }
                                else if (camp.Equals("12"))
                                {
                                    jungleRespawns["Baron"] = xpPacket.time + 420;
                                    junglePos["Baron"] = junglePos2[jungleCreep];
                                }
                            }
                        }
                    });
                }
            }
        }
        static void Drawing_OnDraw(EventArgs args)
        {
            foreach (KeyValuePair<String,Single> creep in jungleRespawns)
            {
                Vector2 pos = Drawing.WorldToMinimap(junglePos[creep.Key]);
                TimeSpan time = TimeSpan.FromSeconds(creep.Value - Game.Time + TimeOffset);
                if (time.TotalSeconds > 0)
                {
                    string display = string.Format("{0}:{1:D2}", time.Minutes, time.Seconds);
                    Drawing.DrawText(pos.X - display.Length * 3, pos.Y - 5, System.Drawing.Color.Yellow, display);
                }
            }
        }
    }
}
