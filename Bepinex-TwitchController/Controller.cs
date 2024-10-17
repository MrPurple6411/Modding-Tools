namespace TwitchController
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using TwitchController.Player_Events;

    public class Controller
    {
        public const string Version = "0.0.0.2";

        internal static readonly Secrets _secrets;
        internal readonly TwitchEventManager eventManager;
        internal readonly TimerCooldown timer;

        internal TwitchChatClient client;
        internal TwitchPubSubClient pubsub;
        internal Channel TextChannel;
        internal Channel PubSubChannel;
        internal System.Threading.CancellationToken cts;
        internal System.Threading.CancellationToken cts2;

        private static Controller _instance;
        public static Controller Instance => _instance ?? (_instance = new Controller());
        private static bool loggedIn;
        public static bool LoggedIn => loggedIn && _secrets.IsValid();

        public readonly EventLookup eventLookup;
        public bool HypeTrain = false;
        public int HypeLevel = 1;
        public int HypeTrainEventCost = 0;
        private static bool loggingIn;

        static Controller()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TwitchConfig.json");
            _secrets = new Secrets(path);
        }

        private Controller()
        {
            eventLookup = new EventLookup(this);
            eventManager = new TwitchEventManager(this);
            timer = new TimerCooldown(this);
        }

        /// <summary>
        /// Launches a HTTPListener on localhost:3000 and then opens the twitch authorization page to get users authorization.
        /// Then when the redirect happens it will detect the users auth token from the address bar.
        /// It then uses the authToken to get the user id and username to be able to connect the websockets to the channel.
        /// None of this data gets stored on disk at any time.
        /// Which is why the popup will happen every time the start is called for the first time in the applications run.
        /// HTTPListener will timeout after 3 minutes if the user has not accepted by then they will need to try to connect again.
        /// </summary>
        private static async Task Login()
        {
            loggingIn = true;
            // Create a Http server and start listening for incoming connections
            var listener = new HttpListener();
            string url = "http://localhost:3000/";

            string unFormattedAuth = "https://id.twitch.tv/oauth2/authorize?response_type=token&client_id={0}&redirect_uri={1}&scope={2}&state={3}";
            string scope = "bits:read channel:read:hype_train channel:read:redemptions channel:read:subscriptions chat:read chat:edit";
            string sentState = new Random().Next(0, 100000).ToString();
            string authUrl = string.Format(unFormattedAuth, _secrets.client_id, url, WebUtility.UrlEncode(scope), sentState);
            Process.Start(authUrl);

            string pageData =
                "<!DOCTYPE>" +
                "<html>" +
                "  <head>" +
                "    <title>Twitch Integration Mod Controller</title>" +
                "  </head>" +
                "  <body class=\"Flex\" style=\"background-color:black;\">" +
                "  <div style=\"position: absolute; top: 50 %; left: 50 %; margin - top: -50px; margin - left: -50px; width: 100px; height: 100px;\">" +
                "      Please Wait while the response is processed." +
                "  </div>" +
                "  </body>" +
                "<script>const o=window.location.hash;window.location.href='http://localhost:3000/?'+o.substring(1);</script>" +
                "</html>";
            listener.Prefixes.Add(url);
            listener.Start();

            Console.WriteLine($"Listening for connections on {url}");
            bool runServer = true;

            DateTime time = DateTime.Now.AddMinutes(3);

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer && DateTime.Now < time)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                if (req.Url.AbsolutePath != "/favicon.ico")
                {
                    string query = req.Url.Query ?? "";
                    if (query.Contains("access_token="))
                    {
                        query = query.Replace("?", "");

                        string[] queryParts = query.Split('&');
                        string code = queryParts[0].Split('=')[1];
                        string state = queryParts[2].Split('=')[1];

                        if (state != sentState)
                        {
                            Console.WriteLine($"State mismatch: {state} != {sentState}");
                            resp.StatusCode = (int)HttpStatusCode.BadRequest;
                            resp.Close();
                            continue;
                        }

                        _secrets.access_token = code;

                        string apiUrl = "https://api.twitch.tv/helix/users";

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                        request.Method = "GET";
                        request.Headers.Add("Authorization", "Bearer " + _secrets.access_token);
                        request.Headers.Add("client-id", _secrets.client_id);
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            StreamReader reader = new StreamReader(response.GetResponseStream());
                            string jsonResponse = reader.ReadToEnd();
                            JObject objectResponse = JsonConvert.DeserializeObject<JObject>(jsonResponse);
                            _secrets.id = (string)objectResponse["data"][0]["id"];
                            _secrets.username = (string)objectResponse["data"][0]["login"];
                            pageData = "<!DOCTYPE><html><head></head><body><b>DONE!</b><br>(Please close this tab/window)</body></html>";
                        }
                        catch (Exception e)
                        {
                            pageData = $"<!DOCTYPE><html><head></head><body><b>Error!</b><br>{e}</body></html>";
                            Console.WriteLine(e.ToString());
                        }
                        runServer = false;
                    }
                    else if (query.Contains("error=access_denied"))
                    {
                        Console.WriteLine($"[Error] Access denied");
                        runServer = false;
                    }
                }
                byte[] data = Encoding.UTF8.GetBytes(pageData);
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }

            Console.WriteLine($"Twitch Authorization Successful: {_secrets.IsValid()}");

            // Close the listener
            listener.Close();

            if (_secrets.IsValid())
            {
                loggedIn = true;
            }
            loggingIn = false;
        }

        public async void Update()
        {
            if (!_secrets?.IsValid() ?? true)
                return;

            if (pubsub?.IsClientConnected() ?? false)
            {
                if (!pubsub.IsChannelConnected(_secrets.username, out PubSubChannel))
                {
                    PubSubChannel = pubsub.JoinChannel(_secrets.username);
                    PubSubChannel.MessageReceived += eventManager.PubSubMessageReceived;
                }
            }

            if (client?.IsClientConnected() ?? false)
            {
                if (!client.IsChannelConnected(_secrets.username, out TextChannel))
                {
                    TextChannel = await client.JoinChannelAsync(_secrets.username, cts);
                    TextChannel.MessageReceived += eventManager.ChatMessageReceived;
                    await TextChannel.SendMessageAsync($"ModBot Connected.", cts);
                }
            }


            if ((!client?.IsClientConnected() ?? true) && (!pubsub?.IsClientConnected() ?? true))
            {
                return;
            }

            timer.Update();
        }

        public bool IsChatClientConnected()
        {
            if (client is null) return false;

            if (!client.IsClientConnected()) 
                return false;

            //Console.WriteLine("Chat Client Connected");

            if (!client.IsChannelConnected(_secrets.username, out TextChannel)) 
                return false;
            
            //Console.WriteLine($"Chat Channel {TextChannel.Name} Connected");
            return true;
        }

        public async Task StartTwitchChatClient()
        {
            if (!LoggedIn)
            {
                if (!loggingIn)
                {
                    loggingIn = true;
                    Task.Factory.StartNew(async () => await Login()).Wait();
                }

                while (loggingIn)
                    await Task.Yield();

                if (!LoggedIn)
                    return;
            }

            try
            {
                if (client is null)
                {
                    cts = new System.Threading.CancellationToken();
                    client = new TwitchChatClient(this);
                }

                if (!client.IsClientConnected())
                {
                    await client.ConnectAsync("oauth:" + _secrets.access_token, _secrets.username, cts);
                    TextChannel = await client.JoinChannelAsync(_secrets.username, cts);
                    TextChannel.MessageReceived += eventManager.ChatMessageReceived;
                    await TextChannel.SendMessageAsync($"ModBot Connected.", cts);
                    Console.WriteLine("ModBot Connected");
                }
                else if (!client.IsChannelConnected(_secrets.username, out TextChannel))
                {
                    TextChannel = await client.JoinChannelAsync(_secrets.username, cts);
                    TextChannel.MessageReceived += eventManager.ChatMessageReceived;
                    await TextChannel.SendMessageAsync($"ModBot Connected.", cts);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task StopTwitchChatClient()
        {
            if (!LoggedIn)
                return;

            if (client is null || !client.IsClientConnected())
            {
                return;
            }

            if (client.IsChannelConnected(_secrets.username, out TextChannel))
            {
                TextChannel.MessageReceived -= eventManager.ChatMessageReceived;
                await TextChannel.SendMessageAsync($"ModBot Disconnecting.", cts);
                await TextChannel.LeaveChannelAsync(cts);
                await client.DisconnectAsync(cts);
                Console.WriteLine("ModBot Disconnected.");
            }
            else
            {
                await client.DisconnectAsync(cts);
                Console.WriteLine("ModBot Disconnected.");
            }
        }

        public bool IsPubSubClientConnected()
        {
            if (pubsub is null) return false;

            if (!pubsub.IsClientConnected())
                return false;

            //Console.WriteLine("PubSub Client Connected");

            if (!pubsub.IsChannelConnected(_secrets.username, out PubSubChannel))
                return false;

            //Console.WriteLine($"PubSub Channel {PubSubChannel.Name} Connected");
            return true;
        }

        public async Task StartTwitchPubSubClient()
        {
            if (!LoggedIn)
            {
                if (!loggingIn)
                {
                    loggingIn = true;
                    Task.Factory.StartNew(async () => await Login()).Wait();
                }

                while (loggingIn)
                    await Task.Yield();

                if (!LoggedIn)
                    return;
            }

            try
            {
                if (pubsub is null)
                {
                    cts2 = new System.Threading.CancellationToken();
                    pubsub = new TwitchPubSubClient(this);
                }

                if (!pubsub.IsClientConnected())
                {
                    await pubsub.ConnectAsync(_secrets.access_token, _secrets.id, cts2);
                    PubSubChannel = pubsub.JoinChannel(_secrets.username);
                    PubSubChannel.MessageReceived += eventManager.PubSubMessageReceived;
                }
                else if (!pubsub.IsChannelConnected(_secrets.username, out PubSubChannel))
                {
                    PubSubChannel = pubsub.JoinChannel(_secrets.username);
                    PubSubChannel.MessageReceived += eventManager.PubSubMessageReceived;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task StopTwitchPubSubClient()
        {
            if (!LoggedIn)
                return;

            if (pubsub is null)
            {
                cts2 = new System.Threading.CancellationToken();
                pubsub = new TwitchPubSubClient(this);
            }


            if (pubsub.IsChannelConnected(_secrets.username, out TextChannel))
            {
                PubSubChannel.MessageReceived -= eventManager.PubSubMessageReceived;
                await PubSubChannel.LeaveChannelAsync(cts2);
                await pubsub.DisconnectAsync(cts2);
            }
            else
            {
                await pubsub.DisconnectAsync(cts2);
            }
        }
    }
}