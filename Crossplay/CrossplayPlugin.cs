﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI.Hooks;
using TShockAPI;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class CrossplayPlugin : TerrariaPlugin
    {
        private readonly List<int> _allowedVersions = new List<int>()
        {
            269,
            270,
            271,
            272,
            273,
            274
        };

        private readonly int[] _clientVersions = new int[Main.maxPlayers];

        private static int _serverVersion;

        public static CrossplayConfig Config = new CrossplayConfig();

        public static string ConfigPath => Path.Combine(TShock.SavePath, "Crossplay.json");
        public override string Name
            => "Crossplay";

        public override string Author
            => "TBC Developers";

        public override string Description
            => "Enables crossplay between mobile and PC clients";

        public override Version Version
            => new Version(1, 0);

        public CrossplayPlugin(Main game)
            : base(game)
        {
            Order = -1;
        }

        public override void Initialize()
        {
            if (!File.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }
            bool writeConfig = true;
            if (File.Exists(ConfigPath))
            {
                Config.Read(ConfigPath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(ConfigPath);
            }

            if (Config.Settings.FakeVersionEnabled)
            {
                _serverVersion = Config.Settings.FakeVersion;
            }

            if (Config.Settings.EnableJourneySupport)
            {
                Main.GameMode = 3;
                
            }

            Commands.ChatCommands.Add(new Command("av.jmunlock", FixJourney, "fj"));

            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        private void FixJourney(CommandArgs args)
        {
            TSPlayer Player = args.Player;

            if(Config.Settings.EnableJourneySupport == true && Config.Settings.allowJourneyUse == true)
            {
                var diff = new BitsByte();
                diff[0] = false;
                diff[1] = false;
                diff[2] = false;
                diff[3] = false;
                diff[4] = false;
                diff[5] = false;
                diff[6] = false;
                diff[7] = false;

                diff[8] = true;

                var packet = new PacketFactory()
                    .SetType((byte)PacketTypes.PlayerInfo)
                    .PackByte(((byte)Player.Index))
                    .PackByte((byte)Player.PlayerData.hair)
                    .PackString(Player.Name)
                    .PackByte(Player.PlayerData.hairDye)
                    .PackBool(Player.PlayerData.hideVisuals[0])
                    .PackBool(Player.PlayerData.hideVisuals[1])
                    .PackBool(Player.PlayerData.hideVisuals[2])
                    .PackByte(((byte)Player.PlayerData.hairColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.skinColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.eyeColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.shirtColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.underShirtColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.pantsColor.Value.packedValue))
                    .PackByte(((byte)Player.PlayerData.shoeColor.Value.packedValue))
                    .PackByte(diff)
                    .PackByte(diff)
                    .PackByte(diff)
                    .PackByte(diff)
                    .GetByteData();
                Player.SendRawData(packet);
                

            }

        }

        private void OnGetData(GetDataEventArgs args)
        {
            MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);

            int index = args.Msg.whoAmI;
            BinaryReader reader = new BinaryReader(stream);

            switch (args.MsgID)
            {
                case PacketTypes.ConnectRequest:
                    {
                        string clientVersion = reader.ReadString();

                        if (!int.TryParse(clientVersion.Substring(clientVersion.Length - 3), out int versionNum))
                            return;

                        if (versionNum == _serverVersion)
                            return;

                        if (!_allowedVersions.Contains(versionNum) && !Config.Settings.FakeVersionEnabled)
                            return;
                        
                        _clientVersions[index] = versionNum;
                        NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Different version detected. Patching..."), 1);

                        byte[] connectRequest = new PacketFactory()
                            .SetType(1)
                            .PackString($"Terraria{_serverVersion}")
                            .GetByteData();

                        Log($"[Crossplay] Changing version of index {args.Msg.whoAmI} from {ParseVersion(versionNum)} => {ParseVersion(_serverVersion)}", ConsoleColor.Magenta);

                        Buffer.BlockCopy(connectRequest, 0, args.Msg.readBuffer, args.Index - 3, connectRequest.Length);
                    }
                    break;
                case PacketTypes.PlayerInfo:
                    {
                        var length = args.Length - 1;
                        Console.WriteLine(length);

                        var bitsbyte = (BitsByte)args.Msg.readBuffer[length];

                        if (Config.Settings.EnableJourneySupport == true && !bitsbyte[3])
                        {
                           if(Main.GameMode == 3)
                            {
                                bitsbyte[0] = false;
                                bitsbyte[1] = false;

                                bitsbyte[3] = true;

                                args.Msg.readBuffer[length] = bitsbyte;

                                Log($"[Crossplay] {(bitsbyte[3] ? "Enabled" : "Disabled")} journeymode for index {args.Msg.whoAmI}.", ConsoleColor.Magenta);

                            }


                         }
                        else if(Config.Settings.EnableJourneySupport == true && bitsbyte[3])
                        {
                            if(Main.GameMode != 3)
                            {
                                bitsbyte[3] = false;
                                bitsbyte[2] = true;
                                args.Msg.readBuffer[length] = bitsbyte;

                                Log($"[Crossplay] {(bitsbyte[3] ? "Enabled" : "Disabled")} journeymode for index {args.Msg.whoAmI}.", ConsoleColor.Magenta);

                            }
                        }
                        
                    }
                    break;
            }
        }

        private void OnLeave(LeaveEventArgs args)
            => _clientVersions[args.Who] = 0;

        private static void Log(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private string ParseVersion(int version)
        {
            switch (version)
            {
                case 269:
                    return "v1.4.4";
                case 270:
                    return "v1.4.4.1";
                case 271:
                    return "v1.4.4.2";
                case 272:
                    return "v1.4.4.3";
                case 273:
                    return "v1.4.4.4";
                case 274:
                    return "v1.4.4.5";


            }
            return $"Unknown{version}";
        }

    }
}