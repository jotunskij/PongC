using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Commons
{
    public class IVector2 : IEquatable<IVector2>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public IVector2(int x, int y)
        {
            X = x;
            Y = y;
        }


        public bool Equals(IVector2 other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IVector2) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }

    public static class Config
    {
        public static IPAddress ServerIP = IPAddress.Parse("127.0.0.1");
        public const int ServerPort = 11000;
        public const int BufferSize = 1024;
        public const int NrOfBalls = 1;
        public const int BallSpeed = 8;
        public const int WindowWidth = 1024;
        public const int WindowHeight = 768;
        public const int PaddleHeight = 100;
        public const int PaddleWidth = 20;
        public const int PaddleSpeed = 10;
        public const int BallDefaultRadius = 25;
        // Run at ´30 ticks/second
        public const int MsPerTick = 1000 / 30;
    }

    public class Player
    {
        public Socket Socket { get; set; }
        public int Number { get; set; }
        public byte[] Buffer = new byte[Config.BufferSize];
        public StringBuilder sb = new();
        // The position is in the top left
        public IVector2 Position { get; set; }
        public IVector2 LastPosition { get; set; } = new (-1, -1);
        public int Score { get; set; }

        public bool IsFirstPlayer => Number == 1;
        public bool IsSecondPlayer => !IsFirstPlayer;

        public void ResetBuffer()
        {
            sb.Clear();
            Buffer = new byte[Config.BufferSize];
        }
    }

    public class Ball
    {
        public int Number { get; set; }
        // The position is in the center
        public IVector2 Position { get; set; }
        public int Radius { get; set; }
        public IVector2 Speed { get; set; }
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
        public int Number { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int Radius { get; set; }
        public int SpeedX { get; set; }
        public int SpeedY { get; set; }
        public int PlayerOneScore { get; set; }
        public int PlayerTwoScore { get; set; }
    }

    public static class Network
    {

        public static List<Message> ParseMessage(string message)
        {
            var messages = new List<Message>();
            message = message.Trim();
            foreach (var m in message.Split("#", StringSplitOptions.RemoveEmptyEntries))
            {
                var matches = PlayerAssigmentRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.PLAYER_ASSIGNMENT,
                        Number = int.Parse(matches[0].Groups[1].Value)
                    });
                }
                matches = PaddlePositionRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.PADDLE_POSITION,
                        Number = int.Parse(matches[0].Groups[1].Value),
                        PositionX = int.Parse(matches[0].Groups[2].Value),
                        PositionY = int.Parse(matches[0].Groups[3].Value)
                    });
                }
                matches = BallPositionRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.BALL_POSITION,
                        Number = int.Parse(matches[0].Groups[1].Value),
                        PositionX = int.Parse(matches[0].Groups[2].Value),
                        PositionY = int.Parse(matches[0].Groups[3].Value)
                    });
                }
                matches = BallAddRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.BALL_ADD,
                        Number = int.Parse(matches[0].Groups[1].Value),
                        PositionX = int.Parse(matches[0].Groups[2].Value),
                        PositionY = int.Parse(matches[0].Groups[3].Value),
                        Radius = int.Parse(matches[0].Groups[4].Value),
                        SpeedX = int.Parse(matches[0].Groups[5].Value),
                        SpeedY = int.Parse(matches[0].Groups[6].Value)
                    });
                }
                matches = BallRemoveRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.BALL_REMOVE,
                        Number = int.Parse(matches[0].Groups[1].Value)
                    });
                }
                matches = ScoreUpdateRE.Matches(message);
                if (matches.Count > 0)
                {
                    messages.Add(new Message()
                    {
                        MessageType = Message.TypeEnum.SCORE_UPDATE,
                        PlayerOneScore = int.Parse(matches[0].Groups[1].Value),
                        PlayerTwoScore = int.Parse(matches[0].Groups[2].Value)
                    });
                }
            }

            return messages;
        }

        public static Regex PlayerAssigmentRE = new (@"^PLAYER ([1,2])#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string PlayerAssigmentCmd(int playerNr)
        {
            return $"PLAYER {playerNr}#";
        }

        public static Regex PaddlePositionRE = new (@"^PADDLE ([1,2]),(\d+),(\d+)#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string PaddlePositionCmd(Player player)
        {
            return $"PADDLE {player.Number},{player.Position.X},{player.Position.Y}#";
        }

        public static Regex BallPositionRE = new (@"^BALLPOS (\d+),(\d+),(\d+)#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallPositionCmd(Ball ball)
        {
            return $"BALLPOS {ball.Number},{ball.Position.X},{ball.Position.Y}#";
        }

        public static Regex BallAddRE = new (@"^BALLADD (\d+),(\d+),(\d+),(\d+),(-?\d+),(-?\d+)#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallAddCmd(Ball ball)
        {
            return $"BALLADD {ball.Number},{ball.Position.X},{ball.Position.Y},{ball.Radius},{ball.Speed.X},{ball.Speed.Y}#";
        }

        public static Regex BallRemoveRE = new (@"^BALLREM (\d+)#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string BallRemoveCmd(int ballNr)
        {
            return $"BALLREM {ballNr}#";
        }

        public static Regex ScoreUpdateRE = new (@"^SCORE (\d+),(\d+)#$", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        public static string ScoreUpdateCmd(int player1Score, int player2Score)
        {
            return $"SCORE {player1Score},{player2Score}#";
        }

    }
}
