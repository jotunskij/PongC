using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Commons
{
    public static class Config
    {
        public static IPAddress ServerIP = IPAddress.Parse("127.0.0.1");
        public const int ServerPort = 11000;
        public const int BufferSize = 1024;
        public const int MaxPlayers = 2;
        public const int MaxBalls = 10;
        public const int WindowWidth = 1024;
        public const int WindowHeight = 768;
        public const int PaddleHeight = 70;
        public const int PaddleWidth = 20;
        public const int BallDefaultRadius = 10;
        public const int MsPerTick = 1000 / 60;
    }

    public class Player
    {
        public Socket Socket { get; set; }
        public int Number { get; set; }
        public byte[] Buffer = new byte[Config.BufferSize];
        public StringBuilder sb = new StringBuilder();
        public Tuple<int, int> Position { get; set; }
        public int Score { get; set; }
    }

    public class Ball
    {
        public int Number { get; set; }
        public Tuple<int, int> Position { get; set; }
        public int Radius { get; set; }
    }

    public class Message
    {
        public enum TypeEnum
        {
            PLAYER_ASSIGNMENT,
            PADDLE_POSITION,
            BALL_POSITION,
            BALL_ADD,
            BALL_REMOVE,
            SCORE_UPDATE
        }
        
        public TypeEnum MessageType { get; set; }
        public int Parameter1 { get; set; }
        public int Parameter2 { get; set; }
        public int Parameter3 { get; set; }
        public int Parameter4 { get; set; }
    }

    public static class Network
    {

        public static Message ParseMessage(string message)
        {
            var matches = PlayerAssigmentRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.PLAYER_ASSIGNMENT,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value)
                };
            }
            matches = PaddlePositionRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.PADDLE_POSITION,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value),
                    Parameter2 = int.Parse(matches[0].Groups[2].Value),
                    Parameter3 = int.Parse(matches[0].Groups[3].Value)
                };
            }
            matches = BallPositionRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.BALL_POSITION,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value),
                    Parameter2 = int.Parse(matches[0].Groups[2].Value),
                    Parameter3 = int.Parse(matches[0].Groups[3].Value)
                };
            }
            matches = BallAddRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.BALL_ADD,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value),
                    Parameter2 = int.Parse(matches[0].Groups[2].Value),
                    Parameter3 = int.Parse(matches[0].Groups[3].Value),
                    Parameter4 = int.Parse(matches[0].Groups[4].Value)
                };
            }
            matches = BallRemoveRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.BALL_REMOVE,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value)
                };
            }
            matches = ScoreUpdateRE.Matches(message);
            if (matches.Count > 0)
            {
                return new Message()
                {
                    MessageType = Message.TypeEnum.SCORE_UPDATE,
                    Parameter1 = int.Parse(matches[0].Groups[1].Value),
                    Parameter2 = int.Parse(matches[0].Groups[2].Value)
                };
            }
            return null;
        }

        public static Regex PlayerAssigmentRE = new Regex(@"PLAYER ([1,2])#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string PlayerAssigmentCmd(int playerNr)
        {
            return $"PLAYER {playerNr}#";
        }

        public static Regex PaddlePositionRE = new Regex(@"PADDLE ([1,2]),(\d),(\d)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string PlayerPositionCmd(int playerNr, int posX, int posY)
        {
            return $"PADDLE {playerNr},{posX},{posY}#";
        }

        public static Regex BallPositionRE = new Regex(@"BALLPOS (\d),(\d),(\d)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallPositionCmd(int ballNr, int posX, int posY)
        {
            return $"BALLPOS {ballNr},{posX},{posY}#";
        }

        public static Regex BallAddRE = new Regex(@"BALLADD (\d),(\d),(\d),(\d)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallAddCmd(int ballNr, int posX, int posY, int radius)
        {
            return $"BALLADD {ballNr},{posX},{posY},{radius}#";
        }

        public static Regex BallRemoveRE = new Regex(@"BALLREM (\d)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallRemoveCmd(int ballNr)
        {
            return $"BALLREM {ballNr}#";
        }

        public static Regex ScoreUpdateRE = new Regex(@"SCORE (\d),(\d)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string ScoreUpdateCmd(int player1Score, int player2Score)
        {
            return $"SCORE {player1Score},{player2Score}#";
        }

    }
}
