using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using Commons;

namespace Server
{
    public static class GameObject
    {
        public static Player PlayerOne { get; set; }
        public static Player PlayerTwo { get; set; }
        public static List<Ball> Balls { get; set; } = new();

        public static bool HasPlayers()
        {
            return PlayerOne != null && PlayerTwo != null;
        }

        public static void HandleCollisions()
        {
            foreach (var b in Balls)
            {
                // Ceiling or floor bounce - invert y-speed
                if (b.Position.Item2 - b.Radius <= 0 || b.Position.Item2 + b.Radius >= Config.WindowHeight)
                    b.SpeedVector = new Tuple<int, int>(b.SpeedVector.Item1, b.SpeedVector.Item2 * -1);
                // Paddle collision - invert x-speed
                if (PlayerBallCollision(PlayerOne, b) || PlayerBallCollision(PlayerTwo, b))
                    b.SpeedVector = new Tuple<int, int>(b.SpeedVector.Item1 * -1, b.SpeedVector.Item2);
            }
        }

        public static List<Ball> GetPlayerOneScoringBalls()
        {
            var scoringBalls = new List<Ball>();
            foreach (var b in Balls)
            {
                // Player 2 scores
                if (b.Position.Item1 - b.Radius >= Config.WindowWidth)
                {
                    scoringBalls.Add(b);
                }
            }

            return scoringBalls;

        }

        public static List<Ball> GetPlayerTwoScoringBalls()
        {
            var scoringBalls = new List<Ball>();
            foreach (var b in Balls)
            {
                // Player 2 scores
                if (b.Position.Item1 - b.Radius <= 0)
                {
                    scoringBalls.Add(b);
                }
            }

            return scoringBalls;
        }

        private static bool PlayerBallCollision(Player p, Ball b)
        {
            Tuple<int, int> ballEdge;
            Tuple<int, int> playerEdgeTop;
            Tuple<int, int> playerEdgeBottom;

            if (p.Position == null)
                return false;

            if (p.IsFirstPlayer)
            {
                // Edge is ball left most point
                ballEdge = new Tuple<int, int>(b.Position.Item1 - b.Radius, b.Position.Item2);
                // Left player, so add width of paddle to X
                playerEdgeTop = new Tuple<int, int>(p.Position.Item1 + Config.PaddleWidth, p.Position.Item2);
                playerEdgeBottom = new Tuple<int, int>(p.Position.Item1 + Config.PaddleWidth, p.Position.Item2 + Config.PaddleHeight);
                // It's past the X of the paddle, and between the top and bottom - collision
                return ballEdge.Item1 <= playerEdgeBottom.Item1 &&
                       ballEdge.Item2 >= playerEdgeTop.Item2 &&
                       ballEdge.Item2 <= playerEdgeBottom.Item2;
            }
            else
            {
                // Edge is ball right most point
                ballEdge = new Tuple<int, int>(b.Position.Item1 + b.Radius, b.Position.Item2);
                // Right player, so position is already left most edge
                playerEdgeTop = new Tuple<int, int>(p.Position.Item1, p.Position.Item2);
                playerEdgeBottom = new Tuple<int, int>(p.Position.Item1, p.Position.Item2 + Config.PaddleHeight);
                return ballEdge.Item1 >= playerEdgeBottom.Item1 &&
                       ballEdge.Item2 >= playerEdgeTop.Item2 &&
                       ballEdge.Item2 <= playerEdgeBottom.Item2;
            }
        }

        public static Player AddPlayer(Player player)
        {
            if (PlayerOne == null)
            {
                player.Number = 1;
                PlayerOne = player;
                Console.WriteLine("Added player 1");
                return player;
            }
            if (PlayerTwo == null)
            {
                player.Number = 2;
                PlayerTwo = player;
                Console.WriteLine("Added player 2");
                return player;
            }
            Console.WriteLine("All player slots are full");
            return null;
        }

        public static Player GetPlayer(int playerNumber)
        {
            if (PlayerOne.Number == playerNumber)
                return PlayerOne;
            if (PlayerTwo.Number == playerNumber)
                return PlayerTwo;
            return null;
        }

        public static void RemovePlayer(Player player)
        {
            if (PlayerOne == player)
            {
                PlayerOne = null;
                Console.WriteLine("Player 1 removed");
            }
            else if (PlayerTwo == player)
            {
                PlayerTwo = null;
                Console.WriteLine("Player 2 removed");
            }
            else
            {
                Console.WriteLine("Player to remove not currently playing");
            }
        }

        public static Ball AddBall(int radius = Config.BallDefaultRadius)
        {
            var rand = new Random();
            if (Balls.Count < Config.MaxBalls)
            {
                var ball = new Ball()
                {
                    Position = new Tuple<int, int>(0, 0),
                    Number = Balls.Count == 0 ? 1 : Balls.Max(b => b.Number) + 1,
                    Radius = radius,
                    // Either -1 och 1 for both axis
                    SpeedVector = new Tuple<int, int>(
                        rand.Next(0, 1) == 0 ? -1 : 1, 
                        rand.Next(0, 1) == 0 ? -1 : 1
                    )
                };
                Balls.Add(ball);
                Console.WriteLine("Ball added");
                return ball;
            }
            else
            {
                Console.WriteLine("Max # balls reached");
                return null;
            }
        }

        public static Ball GetBall(int ballNr)
        {
            return Balls.FirstOrDefault(b => b.Number == ballNr);
        }

        public static void RemoveBall(Ball ball)
        {
            Balls.Remove(ball);
            Console.WriteLine("Ball removed");
        }
    }

    public class Server
    {
        // Thread signal.  
        public static ManualResetEvent playerConnectedLock = new ManualResetEvent(false);
        public static DateTime LastTick = DateTime.MinValue;

        public static void StartServer()
        {
            // Establish the local endpoint for the socket.  
            var localEndPoint = new IPEndPoint(Config.ServerIP, Config.ServerPort);

            // Create a TCP/IP socket.  
            var listener = new Socket(Config.ServerIP.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    if (GameObject.PlayerOne == null)
                    {
                        // Set the event to nonsignaled state.  
                        playerConnectedLock.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        Console.WriteLine("Waiting for player 1...");
                        listener.BeginAccept(
                            PlayerConnectedCallback,
                            listener);

                        // Wait until a connection is made before continuing.  
                        playerConnectedLock.WaitOne();
                        Send(GameObject.PlayerOne, Network.PlayerAssigmentCmd(GameObject.PlayerOne.Number));
                    }

                    if (GameObject.PlayerTwo == null)
                    {
                        // Set the event to nonsignaled state.  
                        playerConnectedLock.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        Console.WriteLine("Waiting for player 2...");
                        listener.BeginAccept(
                            PlayerConnectedCallback,
                            listener);

                        // Wait until a connection is made before continuing.  
                        playerConnectedLock.WaitOne();
                        Send(GameObject.PlayerTwo, Network.PlayerAssigmentCmd(GameObject.PlayerTwo.Number));
                    }

                    if (GameObject.HasPlayers())
                        RunSimulationTick();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        public static void RunSimulationTick()
        {
            if ((DateTime.Now - LastTick).TotalMilliseconds < Config.MsPerTick)
            {
                LastTick = DateTime.Now;
                return;
            }

            // Add balls up to minimum amount
            if (GameObject.Balls.Count < Config.MinBalls)
            {
                for (var i = GameObject.Balls.Count; i < Config.MinBalls; i++)
                {
                    var ball = GameObject.AddBall();
                    if (ball != null)
                        Broadcast(Network.BallAddCmd(
                            ball.Number,
                            ball.Position.Item1,
                            ball.Position.Item2,
                            ball.Radius,
                            ball.SpeedVector.Item1,
                            ball.SpeedVector.Item2)
                        );
                }
            }

            // Handle ball-paddle/ceiling/floor collisions
            GameObject.HandleCollisions();

            // Handle scoring balls
            var scoringBallsOne = GameObject.GetPlayerOneScoringBalls();
            if (scoringBallsOne.Count > 0)
            {
                GameObject.PlayerOne.Score += scoringBallsOne.Count;
            }
            var scoringBallsTwo = GameObject.GetPlayerTwoScoringBalls();
            if (scoringBallsTwo.Count > 0)
            {
                GameObject.PlayerTwo.Score += scoringBallsTwo.Count;
            }

            // Update clients on removed balls, and remove them
            scoringBallsOne.AddRange(scoringBallsTwo);
            foreach (var b in scoringBallsOne)
            {
                Broadcast(Network.BallRemoveCmd(b.Number));
                GameObject.RemoveBall(b);
            }

            // Update clients on new score
            Broadcast(Network.ScoreUpdateCmd(GameObject.PlayerOne.Score, GameObject.PlayerTwo.Score));

            // Update clients on ball positions
            foreach (var b in GameObject.Balls)
                Broadcast(Network.BallPositionCmd(b.Number, b.Position.Item1, b.Position.Item2));

            LastTick = DateTime.Now;
        }

        public static void PlayerConnectedCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.  
            var listener = (Socket) ar.AsyncState;
            var handler = listener.EndAccept(ar);

            // Create the state object.
            var player = new Player {Socket = handler};
            player = GameObject.AddPlayer(player); 
            Console.WriteLine($"Player {player.Number} connected!");

            // Signal the main thread to continue.  
            playerConnectedLock.Set();

            handler.BeginReceive(player.Buffer, 0, Config.BufferSize, 0,
                ReadCallback, player);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            var content = string.Empty;
            var player = (Player) ar.AsyncState;
            var handler = player.Socket;

            int bytesRead;
            try
            {
                bytesRead = handler.EndReceive(ar);
            }
            catch
            {
                // Handle disconnects
                GameObject.RemovePlayer(player);
                return;
            }

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                player.sb.Append(Encoding.ASCII.GetString(player.Buffer, 0, bytesRead));

                content = player.sb.ToString();
                if (content.IndexOf("#") > -1)
                {
                    Console.WriteLine($"Read {content.Length} bytes from socket. \n Data : {content}");
                    var msg = Network.ParseMessage(content);
                    HandleMessage(msg);
                    player.ResetBuffer();
                }
            }

            // Get more data or next message  
            handler.BeginReceive(player.Buffer, 0, Config.BufferSize, 0,
                ReadCallback, player);
        }

        public static void HandleMessage(Message message)
        {
            if (message == null)
            {
                Console.WriteLine("Invalid message");
                return;
            }

            switch (message.MessageType)
            {
                case Message.TypeEnum.PADDLE_POSITION:
                    Console.WriteLine($"Got PADDLE_POSITION message"); 
                    var player = GameObject.GetPlayer(message.Number);
                    player.Position = new Tuple<int, int>(message.PositionX, message.PositionY);
                    Broadcast(Network.PaddlePositionCmd(player.Number, player.Position.Item1, player.Position.Item2));
                    break;
            }

        }

        private static void Broadcast(string data)
        {
            Send(GameObject.PlayerOne, data);
            Send(GameObject.PlayerTwo, data);
        }

        private static void Send(Player player, string data)
        {
            if (player == null)
                return;
            Console.WriteLine($"Sending {data} to player {player.Number}"); 
            var byteData = Encoding.ASCII.GetBytes(data);
            var handler = player.Socket;

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                SendCallback, handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                var handler = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.  
                var bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static int Main(string[] args)
        {
            StartServer();
            return 0;
        }
    }
}