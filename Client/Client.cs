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
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Player Player { get; set; }
        private Player Opponent { get; set; }
        private List<Ball> Balls { get; set; }

        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);  
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        public Client()
        {
            _graphics = new GraphicsDeviceManager(this);
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
                Trace.WriteLine("Connected to server");

                // Wait for the player assignment message
                Receive();
                receiveDone.WaitOne();

            } catch (Exception e) {  
                Trace.WriteLine(e.ToString());  
            }  
        }

        private void Receive() {
            try {  
                // Begin receiving the data from the remote device.  
                Player.Socket.BeginReceive(Player.Buffer, 0, Config.BufferSize, 0,  
                    ReceiveCallback, Player);  
            } catch (Exception e) {  
                Trace.WriteLine(e.ToString()); 
            }
        }
  
        private void ReceiveCallback(IAsyncResult ar) {  
            try
            {
                var content = string.Empty;
                Trace.WriteLine("In ReceiveCallback");
                // Read data from the remote device.  
                var bytesRead = Player.Socket.EndReceive(ar);
  
                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    Player.sb.Append(Encoding.ASCII.GetString(
                        Player.Buffer, 0, bytesRead));

                    content = Player.sb.ToString();
                    if (content.IndexOf("#") > -1)
                    {
                        Trace.WriteLine($"Received {content}");
                        var msg = Network.ParseMessage(content);
                        HandleMessage(msg);
                    }
                    else
                    {
                        // Not all data received. Get more.  
                        Player.Socket.BeginReceive(Player.Buffer, 0, Config.BufferSize, 0,
                            ReceiveCallback, Player);
                    }
                }  
            } catch (Exception e) {  
                Trace.WriteLine(e.ToString());  
            }  
        }

        private void Send(string data)
        {
            Trace.WriteLine($"Sending {data}");
            var byteData = Encoding.ASCII.GetBytes(data);  
  
            Player.Socket.BeginSend(byteData, 0, byteData.Length, 0,  
                SendCallback, Player);  
        }  
  
        private void SendCallback(IAsyncResult ar) {  
            try {  
                // Complete sending the data to the remote device.  
                var bytesSent = Player.Socket.EndSend(ar);  
                Trace.WriteLine($"Sent {bytesSent} bytes to server.");  
  
                // Signal that all bytes have been sent.  
                sendDone.Set();
            } catch (Exception e) {  
                Trace.WriteLine(e.ToString());  
            }  
        } 

        private void ConnectCallback(IAsyncResult ar) {  
            try {  
                // Retrieve the socket from the state object.
                var client = (Socket) ar.AsyncState;  
  
                // Complete the connection. 
                client.EndConnect(ar);
                Player = new Player()
                {
                    Socket = client
                };
  
                Trace.WriteLine($"Socket connected to {client.RemoteEndPoint}");
  
                // Signal that the connection has been made.
                connectDone.Set();
            } catch (Exception e) {  
                Trace.WriteLine(e.ToString());  
            }  
        }

        private void SendPosition()
        {
            Send(Network.PlayerPositionCmd(Player.Number, Player.Position.Item1, Player.Position.Item2));
        }

        public void HandleMessage(Message message)
        {
            if (message == null)
                return;
            if (message.MessageType == Message.TypeEnum.PLAYER_ASSIGNMENT)
            {
                Trace.WriteLine($"Got assigned player nr {message.Parameter1}");
                Player.Number = message.Parameter1;
            }
            if (message.MessageType == Message.TypeEnum.BALL_POSITION)
            {
                var ball = Balls.FirstOrDefault(b => b.Number == message.Parameter1);
                if (ball == null)
                    return;
                ball.Position = new Tuple<int, int>(message.Parameter2, message.Parameter3);
            }
            if (message.MessageType == Message.TypeEnum.BALL_ADD)
            {
                Balls.Add(new Ball()
                {
                    Number = message.Parameter1,
                    Position = new Tuple<int, int>(message.Parameter2, message.Parameter3),
                    Radius = message.Parameter4
                });
            }
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = Config.WindowWidth;
            _graphics.PreferredBackBufferHeight = Config.WindowHeight;
            StartClient();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            Receive();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            SendPosition();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}
