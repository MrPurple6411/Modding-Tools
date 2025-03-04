﻿namespace TwitchController
{
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WebSocketPubSubClient : IMessageClient
    {
        public event EventHandler<string> MessageReceived;

        public event EventHandler ConnectionClosed;

        private readonly ClientWebSocket _webSocketClient = new ClientWebSocket();

        private readonly Uri _webSocketServerUri;

        public WebSocketPubSubClient(string webSocketServerUrl = /*"wss://irc.fdgt.dev:443")//*/"wss://pubsub-edge.twitch.tv:443")
        {
            _webSocketServerUri = new Uri(webSocketServerUrl);
        }

        public async Task<bool> SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            var sent = false;
            try
            {
                await _webSocketClient.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                sent = true;
            }
            catch
            {
                sent = false;
            }
            return sent;
        }

        public async Task<bool> ConnectAsync(string token, string channelId, CancellationToken cancellationToken)
        {
            try
            {
                await _webSocketClient.ConnectAsync(_webSocketServerUri, cancellationToken);

                if(_webSocketClient.State == WebSocketState.Open)
                {
                    ListenRequest lr = new ListenRequest();
                    ListenRequestData lrd = new ListenRequestData
                    {
                        auth_token = token,
                        topics = new string[] { "channel-points-channel-v1." + channelId, "channel-bits-events-v2." + channelId, "channel-subscribe-events-v1." + channelId, "hype-train-events-v1." + channelId }
                    };
                    lr.data = lrd;
                    lr.nonce = "lkjsdhfiusdagf";
                    lr.type = "LISTEN";
                    string jlr = JsonConvert.SerializeObject(lr);
                    //Controller._instance._log.LogMessage(jlr);
                    await SendMessageAsync(jlr, cancellationToken);

                    Timer timer = new Timer(async (e) =>
                    {
                        Console.WriteLine("Sending PubSub Ping");
                        await SendMessageAsync("{\"type\":  \"PING\"}", cancellationToken);
                    }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

                    // start receiving messages in separeted thread
                    System.Runtime.CompilerServices.ConfiguredTaskAwaitable receive = ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                }
                return false;
            }
            catch 
            {
                return false;
            }
        }

        public bool IsConnected()
        {
            return _webSocketClient.State == WebSocketState.Open;

        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return _webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", cancellationToken);
        }

        public async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string message = await ReceiveMessageAsync(cancellationToken);
                    MessageReceived?.Invoke(this, message);
                }
                catch (WebSocketException)
                {
                    if (_webSocketClient.State != WebSocketState.Open)
                    {
                        ConnectionClosed?.Invoke(this, null);
                    }
                    _webSocketClient.Abort();
                    return;
                }
            };
        }

        /// <summary>
        /// Receives raw message from the opened connection.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            // RFC 1459 uses 512 bytes to hold one full message, therefore, it should be enough
            byte[] byteArray = new byte[4096];
            ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(byteArray);
            string receivedMessage = "";
            while (true)
            {
                WebSocketReceiveResult receivedResult = await _webSocketClient.ReceiveAsync(receiveBuffer, cancellationToken);
                byte[] msgBytes = receiveBuffer.Skip(receiveBuffer.Offset)
                    .Take(receivedResult.Count)
                    .ToArray();
                receivedMessage += Encoding.UTF8.GetString(msgBytes);
                if (receivedResult.EndOfMessage)
                {
                    break;
                }
            }

            return receivedMessage;
        }
    }
}