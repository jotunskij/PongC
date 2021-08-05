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
        public static List<Ball> Balls { get; set; }

        public static bool HasPlayers()
        {
            return PlayerOne != null && PlayerTwo != null;
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

        public static void AddBall(int radius = Config.BallDefaultRadius)
        {
            if (Balls.Count < Config.MaxBalls)
            {
                var ball = new Ball()
                {
                    Position = new Tuple<int, int>(0, 0),
                    Number = Balls.Max(b => b.Number) + 1,
                    Radius = radius
                };
                Balls.Add(ball);
                Console.WriteLine("Ball added");
            }
            else
            {
                Console.WriteLine("Max # balls reached");
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
        public static ManualResetEvent allDone = new ManualResetEvent(false);
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
                        allDone.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        Console.WriteLine("Waiting for player 1...");
                        listener.BeginAccept(
                            PlayerConnectedCallback,
                            listener);

                        // Wait until a connection is made before continuing.  
                        allDone.WaitOne();
                        Send(GameObject.PlayerOne, Network.PlayerAssigmentCmd(GameObject.PlayerOne.Number));
                    }

                    if (GameObject.PlayerTwo == null)
                    {
                        // Set the event to nonsignaled state.  
                        allDone.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        Console.WriteLine("Waiting for player 2...");
                        listener.BeginAccept(
                            PlayerConnectedCallback,
                            listener);

                        // Wait until a connection is made before continuing.  
                        allDone.WaitOne();
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
            
            // TODO: Check ball/paddle collisions
            // TODO: Check ball past edge of screen
            // TODO: Update scores - send message
            // TODO: Remove dead balls, add new - send message
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
            allDone.Set();

            handler.BeginReceive(player.Buffer, 0, Config.BufferSize, 0,
                ReadCallback, player);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            var content = string.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            var player = (Player) ar.AsyncState;
            var handler = player.Socket;

            // Read data from the client socket.
            var bytesRead = 0;
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
                player.sb.Append(Encoding.ASCII.GetString(
                    player.Buffer, 0, bytesRead));

                content = player.sb.ToString();
                if (content.IndexOf("#") > -1)
                {
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);
                    var msg = Network.ParseMessage(content);
                    HandleMessage(msg);
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(player.Buffer, 0, Config.BufferSize, 0,
                        ReadCallback, player);
                }
            }
        }

        public static void HandleMessage(Message message)
        {
            if (message == null)
            {
                Console.WriteLine("Invalid message");
                return;
            }

            if (message.MessageType == Message.TypeEnum.PADDLE_POSITION)
            {
                var player = GameObject.GetPlayer(message.Parameter1);
                player.Position = new Tuple<int, int>(message.Parameter2, message.Parameter3);
            }
        }

        private static void Send(Player player, string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
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

                //handler.Shutdown(SocketShutdown.Both);
                //handler.Close();
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