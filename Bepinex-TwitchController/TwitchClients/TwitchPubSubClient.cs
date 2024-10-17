namespace TwitchController
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TwitchController.Player_Events.Models;
    using TwitchController.TwitchClients.Models;

    public class TwitchPubSubClient
    {

        public event EventHandler ConnectionClose;

        public event EventHandler<string> MessageRecieved;

        private IMessageClient _twitchMessageClient;

        protected Channel _channel;

        private readonly Controller controller;

        public TwitchPubSubClient(Controller twitchController)
        {
            controller = twitchController;
        }

        public bool IsClientConnected()
        {
            return _twitchMessageClient?.IsConnected() ?? false;
        }

        public bool IsChannelConnected(string channelName, out Channel outchannel)
        {
            outchannel = null;
            if (_channel?.Name == channelName)
            {
                outchannel = _channel;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Triggered when the connection is closed from any reason.
        /// </summary>
        /// <param name="sender">IMessageClient</param>
        /// <param name="e">null</param>
        private void OnConnectionClosed(object sender, EventArgs e)
        {
            ConnectionClose?.Invoke(this, e);
        }

        /// <summary>
        /// Triggered when raw message received.
        /// </summary>
        /// <param name="sender">IMessageClient</param>
        /// <param name="e">Raw message</param>
        private void OnRawMessageReceived(object sender, string e)
        {
            try
            {
                if(this.TryParsePrivateMessage(e, out Message message))
                {
                    _channel?.ReceiveMessage(message);
                }
            }
            catch(Exception ex) 
            {
                Console.WriteLine($"[Error] {ex}");
            }

            try
            {
                MessageRecieved?.Invoke(sender, e);
            }
            catch { }
        }

        /// <summary>
        /// Opens a connection to the server and start receiving messages.
        /// </summary>
        /// <param name="oauth">Your password should be an OAuth token authorized through our API with the chat:read scope (to read messages) and the  chat:edit scope (to send messages)</param>
        /// <param name="nick">Your nickname must be your Twitch username (login name) in lowercase</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ConnectAsync(string oauth, string nickId, CancellationToken cancellationToken)
        {
            _twitchMessageClient = new WebSocketPubSubClient();
            _twitchMessageClient.MessageReceived += OnRawMessageReceived;
            _twitchMessageClient.ConnectionClosed += OnConnectionClosed;

            return _twitchMessageClient.ConnectAsync(oauth, nickId.ToLower(), cancellationToken);
        }

        /// <summary>
        /// Attempts to Reconnect asynchronously.
        /// </summary>
        /// <returns></returns>
        public Task ReConnectAsync()
        {
            return _twitchMessageClient?.ConnectAsync(Controller._secrets.access_token, Controller._secrets.id, controller.cts2) ?? ConnectAsync(Controller._secrets.access_token, Controller._secrets.id, controller.cts2);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_twitchMessageClient != null)
            {
                return _twitchMessageClient.DisconnectAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Joins to given twitch channel. Connection must be established first.
        /// </summary>
        /// <param name="channelName">A channel name</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Channel JoinChannel(string channelName)
        {            
            _channel = new Channel(channelName.ToLower(), _twitchMessageClient);
            return _channel;
        }

        /// <summary>
        /// Tries to parse raw message into message object.
        /// </summary>
        /// <param name="message">Raw message received from a server</param>
        /// <param name="msg">Output message object when successfully parsed</param>
        /// <returns></returns>
        public bool TryParsePrivateMessage(string message, out Message msg)
        {
            msg = new Message();
            JObject data = JsonConvert.DeserializeObject<JObject>(message);

            string msgType = data["type"].ToString().ToLower();
            List<string> keys = data.Properties().Select(x => x.Name).ToList();

            try
            {
                switch (msgType)
                {
                    case "pong":
                        Console.WriteLine($"PubSub Pong Recieved!");
                        return false;
                    case "response":
                        if (keys.Contains("error") && !string.IsNullOrWhiteSpace(data["error"].ToString()))
                        {
                            Console.WriteLine($"[Error] Failed to properly connect to PubSub! Restarting game REQUIRED!");
                        }
                        else
                        {
                            Console.WriteLine($"Connected to PubSub!");
                        }
                        return false;
                    case "reconnect":
                        Console.WriteLine($"[Warning] Twitch Server Restarting connection will be lost within 30 seconds.");
                        _twitchMessageClient.DisconnectAsync(Controller.Instance.cts2).Wait();
                        break;
                    case "message":
                        MessageResponse messageResponse = JsonConvert.DeserializeObject<MessageResponse>(message);
                        var MR = messageResponse.data.message.Replace(@"\", "");
                        var host = messageResponse.data.topic;

                        switch (host.Split('.')[0])
                        {
                            case "channel-subscribe-events-v1":
                                try
                                {
                                    SubEvent subEvent = JsonConvert.DeserializeObject<SubEvent>(MR);

                                    msg.Channel = subEvent.channel_name.ToLower();
                                    msg.Host = host;
                                    msg.RawMessage = message;
                                    msg.TriggerText = subEvent.sub_plan;
                                    msg.User = subEvent.is_gift && !string.IsNullOrEmpty(subEvent.user_name) ? subEvent.recipient_display_name : subEvent.display_name;
                                    msg.Data = subEvent.sub_message.message;

                                    return true;

                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[Error] Failed to convert {MR} into SubEvent.", e);
                                    return false;
                                }
                            case "channel-bits-events-v2":
                                try
                                {
                                    BitsEvent bitsEvent = JsonConvert.DeserializeObject<BitsEvent>(MR);
                                    msg.Channel = bitsEvent.data.channel_name.ToLower();
                                    msg.Host = host;
                                    msg.RawMessage = message;
                                    msg.TriggerText = bitsEvent.data.bits_used.ToString();
                                    msg.User = bitsEvent.is_anonymous ? "Anonymous" : bitsEvent.data.user_name;
                                    msg.Data = bitsEvent.data.chat_message;

                                    return true;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[Error] Failed to convert {MR} into BitsEvent.", e);
                                    return false;
                                }
                            case "channel-points-channel-v1":
                                if (MR.Contains("reward-redeemed"))
                                {
                                    try
                                    {
                                        ChannelPointsMessageResponse pointsMessage = JsonConvert.DeserializeObject<ChannelPointsMessageResponse>(MR);

                                        msg.Host = $"channel-points-channel-v1.{Controller._secrets.id}";
                                        msg.Channel = Controller._secrets.username.ToLower();
                                        msg.RawMessage = message;
                                        msg.User = pointsMessage.data.redemption.user.display_name;
                                        msg.TriggerText = pointsMessage.data.redemption.reward.title;
                                        msg.Data = pointsMessage.data.redemption.user_input;

                                        return true;
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"[Error] Failed to convert {MR} into Points Event.", e);
                                        return false;
                                    }
                                }
                                return false;
                            case "hype-train-events-v1":
                                {
                                    JObject hypeTrainMessage = JsonConvert.DeserializeObject<JObject>(MR);

                                    switch (hypeTrainMessage["type"].ToString())
                                    {
                                        case "hype-train-approaching":
                                        case "hype-train-progression":
                                        case "hype-train-conductor-update":
                                        case "hype-train-cooldown-expiration":

                                            return false;
                                        case "hype-train-start":
                                            {
                                                Controller.Instance.HypeTrain = true;
                                                controller.eventLookup.ChangeCost("HypeTrain", Controller.Instance.HypeTrainEventCost);
                                                controller.eventLookup.Lookup("HypeTrainStart", "!!!HYPETRAIN STARTED!!!");
                                                controller.eventLookup.SendBitsEvents();
                                                return false;
                                            }
                                        case "hype-train-level-up":
                                            {
                                                controller.eventLookup.Lookup($"HypeTrainLevel{Controller.Instance.HypeLevel}Completed", $"!!!LEVEL {Controller.Instance.HypeLevel} HYPETRAIN!!!");
                                                Controller.Instance.HypeLevel += 1;
                                                return false;
                                            }
                                        case "hype-train-end":
                                            {
                                                Controller.Instance.HypeTrain = false;
                                                Controller.Instance.HypeLevel = 1;
                                                controller.eventLookup.ChangeCost("HypeTrain", 0);
                                                controller.eventLookup.Lookup("HypeTrainEnd", $"!!!HYPETRAIN FINISHED!!!");
                                                return false;
                                            }
                                        default:
                                            Console.WriteLine($"Unhandled HypeTrain Event.\n{MR}");
                                            return false;

                                    }
                                }
                            default:
                                Console.WriteLine($"[Error] PubSub Event Failed to Parse \n {message}");
                                return false;
                        }

                    default:
                        break;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] PubSub Event Failed to Parse \n {message}", e);
            }
            return false;
        }
    }
}