using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Commons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client
{

    public class Client : Game
    {
        private GraphicsDeviceManager Graphics { get; set; }
        private SpriteBatch SpriteBatch { get; set; }
        private Player Player { get; set; }
        private Player Opponent { get; set; }
        private List<Ball> Balls { get; set; }
        private Texture2D BallTexture { get; set; }
        private SpriteFont ScoreFont { get; set; }
        private Texture2D PaddleTexture { get; set; }

        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);  
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        public Client()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        private void StartClient() {  
            try
            {
                var ipAddress = Config.ServerIP;  
                var serverEndPoint = new IPEndPoint(ipAddress, Config.ServerPort);  
  
                var client = new Socket(ipAddress.AddressFamily,  
                    SocketType.Stream, ProtocolType.Tcp);  
  
                // Connect to the server
                client.BeginConnect(serverEndPoint,
                    ConnectCallback, client);  
                connectDone.WaitOne();
                Console.WriteLine("Connected to server");

                // Wait for the player assignment message
                Receive();
                receiveDone.WaitOne();

            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }

        private void Receive() {
            try {  
                // Begin receiving the data from the remote device.  
                Player.Socket.BeginReceive(Player.Buffer, 0, Config.BufferSize, 0,  
                    ReceiveCallback, Player);  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString()); 
            }
        }
  
        private void ReceiveCallback(IAsyncResult ar) {  
            try
            {
                var content = string.Empty;
                //Console.WriteLine("In ReceiveCallback");
                // Read data from the remote device.  
                var bytesRead = Player.Socket.EndReceive(ar);
  
                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    Player.sb.Append(Encoding.ASCII.GetString(Player.Buffer, 0, bytesRead));

                    content = Player.sb.ToString();
                    if (content.IndexOf("#") > -1)
                    {
                        //Console.WriteLine($"Received {content}");
                        var messages = Network.ParseMessage(content);
                        HandleMessage(messages);
                        Player.ResetBuffer();
                        receiveDone.Set();
                    }
                }

                // Get more data or next message
                Player.Socket.BeginReceive(Player.Buffer, 0, Config.BufferSize, 0,
                    ReceiveCallback, Player);

            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }

        private void Send(string data)
        {
            //Console.WriteLine($"Sending {data}");
            var byteData = Encoding.ASCII.GetBytes(data);  
  
            Player.Socket.BeginSend(byteData, 0, byteData.Length, 0,  
                SendCallback, Player);  
        }  
  
        private void SendCallback(IAsyncResult ar) {  
            try {  
                // Complete sending the data to the remote device.  
                var bytesSent = Player.Socket.EndSend(ar);  
                //Console.WriteLine($"Sent {bytesSent} bytes to server.");  
  
                // Signal that all bytes have been sent.  
                sendDone.Set();
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        } 

        private void ConnectCallback(IAsyncResult ar) {  
            try {  
                // Retrieve the socket from the state object.
                var client = (Socket) ar.AsyncState;  
  
                // Complete the connection. 
                client.EndConnect(ar);
                Player.Socket = client;
  
                Console.WriteLine($"Socket connected to {client.RemoteEndPoint}");
  
                // Signal that the connection has been made.
                connectDone.Set();
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }

        private void SendPosition()
        {
            if (Player.Position == null)
                return;
            if (!Player.Position.Equals(Player.LastPosition))
                Send(Network.PaddlePositionCmd(Player));
        }

        public void HandleMessage(List<Message> messages)
        {
            if (messages == null || messages.Count == 0)
                return;
            foreach (var message in messages)
            {
                switch (message.MessageType)
                {
                    case Message.TypeEnum.PLAYER_ASSIGNMENT:
                        Console.WriteLine($"Got assigned player nr {message.Number}");
                        Player.Number = message.Number;
                        // Position first player to the left, second to the right
                        if (Player.IsFirstPlayer)
                        {
                            Opponent.Position = new IVector2(Config.WindowWidth - Config.PaddleWidth, Config.WindowHeight / 2);
                            Player.Position = new IVector2(0, Config.WindowHeight / 2);
                        }
                        else
                        {
                            Opponent.Position = new IVector2(0, Config.WindowHeight / 2);
                            Player.Position = new IVector2(Config.WindowWidth - Config.PaddleWidth, Config.WindowHeight / 2);
                        }
                        break;
                    case Message.TypeEnum.BALL_POSITION:
                        var ball = Balls.FirstOrDefault(b => b.Number == message.Number);
                        if (ball == null)
                            return;
                        ball.Position.X = message.PositionX;
                        ball.Position.Y = message.PositionY;
                        break;
                    case Message.TypeEnum.BALL_ADD:
                        Console.WriteLine($"Added new ball {message.Number}");
                        Balls.Add(new Ball()
                        {
                            Number = message.Number,
                            Position = new IVector2(message.PositionX, message.PositionY),
                            Radius = message.Radius,
                            Speed = new IVector2(message.SpeedX, message.SpeedY)
                        });
                        break;
                    case Message.TypeEnum.BALL_REMOVE:
                        Console.WriteLine($"Removed ball {message.Number}");
                        for (var i = Balls.Count - 1; i >= 0; i--)
                        {
                            if (Balls[i].Number == message.Number)
                                Balls.RemoveAt(i);
                        }
                        break;
                    case Message.TypeEnum.PADDLE_POSITION:
                        if (message.Number == Player.Number)
                            break;
                        Opponent.Position.X = message.PositionX;
                        Opponent.Position.Y = message.PositionY;
                        break;
                    case Message.TypeEnum.SCORE_UPDATE:
                        Player.Score = Player.IsFirstPlayer ? message.PlayerOneScore : message.PlayerTwoScore;
                        Opponent.Score = Player.IsFirstPlayer ? message.PlayerTwoScore : message.PlayerOneScore;
                        break;
                }
            }
        }

        protected override void Initialize()
        {
            Graphics.PreferredBackBufferWidth = Config.WindowWidth;
            Graphics.PreferredBackBufferHeight = Config.WindowHeight;
            Graphics.ApplyChanges();
            Player = new Player();
            Opponent = new Player();
            Balls = new List<Ball>();
            IsFixedTimeStep = true;  //Force the game to update at fixed time intervals
            TargetElapsedTime = TimeSpan.FromSeconds(1 / 30.0f);  //Set the time interval to 1/30th of a second
            StartClient();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            PaddleTexture = Content.Load<Texture2D>("paddle");
            BallTexture = Content.Load<Texture2D>("ball");
            ScoreFont = Content.Load<SpriteFont>("score");
        }

        protected override void Update(GameTime gameTime)
        {
            HandleInput();
            SendPosition();
            UpdateLastPosition();
            base.Update(gameTime);
        }

        private void UpdateLastPosition()
        {
            if (Player.Position == null)
                return;
            Player.LastPosition.X = Player.Position.X;
            Player.LastPosition.Y = Player.Position.Y;
        }

        protected void HandleInput()
        {
            // Player 2 down
            if (Keyboard.GetState().IsKeyDown(Keys.Down) && Player.IsSecondPlayer)
                UpdatePlayerPosition(Config.PaddleSpeed);
            // Player 2 up
            if (Keyboard.GetState().IsKeyDown(Keys.Up) && Player.IsSecondPlayer)
                UpdatePlayerPosition(-Config.PaddleSpeed);
            // Player 1 down
            if (Keyboard.GetState().IsKeyDown(Keys.Z) && Player.IsFirstPlayer)
                UpdatePlayerPosition(Config.PaddleSpeed);
            // Player 1 up
            if (Keyboard.GetState().IsKeyDown(Keys.A) && Player.IsFirstPlayer)
                UpdatePlayerPosition(-Config.PaddleSpeed);
        }

        protected void UpdatePlayerPosition(int delta)
        {
            var clampedYPos = 
                Math.Clamp(Player.Position.Y + delta, 0, Config.WindowHeight - Config.PaddleHeight);
            Player.Position.X = Player.Position.X;
            Player.Position.Y = clampedYPos;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            SpriteBatch.Begin();

            // Draw player
            SpriteBatch.Draw(
                PaddleTexture,
                new Vector2(Player.Position.X, Player.Position.Y), 
                Color.White
            );

            // Draw opponent
            SpriteBatch.Draw(
                PaddleTexture, 
                new Vector2(Opponent.Position.X, Opponent.Position.Y), 
                Color.White
            );

            // Draw balls
            foreach (var b in Balls)
            {
                SpriteBatch.Draw(
                    BallTexture,
                    new Vector2(b.Position.X - b.Radius, b.Position.Y - b.Radius), 
                    null, 
                    Color.White, 
                    0f, 
                    Vector2.Zero, 
                    (float) b.Radius / Config.BallDefaultRadius, // Scale factor
                    SpriteEffects.None, 
                    0f
                );
            }

            // Draw score
            var firstPlayerScore = Player.IsFirstPlayer ? Player.Score : Opponent.Score;
            var secondPlayerScore = Player.IsFirstPlayer ? Opponent.Score : Player.Score;
            SpriteBatch.DrawString(
                ScoreFont, 
                firstPlayerScore.ToString(), 
                new Vector2((Config.WindowWidth / 3) * 1, 50), 
                Color.LightGray
            );
            SpriteBatch.DrawString(
                ScoreFont, 
                secondPlayerScore.ToString(), 
                new Vector2((Config.WindowWidth / 3) * 2, 50), 
                Color.LightGray
            );

            SpriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
