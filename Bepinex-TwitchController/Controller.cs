namespace TwitchController
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using LitJson;
    using TwitchController.Player_Events;

    public class Controller
    {

        public const string Version = "0.0.0.1";

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
        private bool loggedIn;

        public readonly EventLookup eventLookup;
        public bool HypeTrain = false;
        public int HypeLevel = 1;

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
            Task.Factory.StartNew(async () => await Login()).Wait();
        }
        
        private async Task Login()
        {
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
                "    <title>MrPurple's Mod Controller</title>" +
                "  </head>" +
                "  <body class=\"Flex\" style=\"background-color:black;\">" +
                "  <div style=\"position: absolute; top: 50 %; left: 50 %; margin - top: -50px; margin - left: -50px; width: 100px; height: 100px;\">" +
                "      Please Wait while the response is processed." +
                "  </div>" +
                "  </body>"+
                "<script>const o=window.location.hash;window.location.href='http://localhost:3000/?'+o.substring(1);</script>" +
                "</html>";
            listener.Prefixes.Add(url);
            listener.Start();

            Console.WriteLine($"Listening for connections on {url}");
            bool runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
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



                            var objectResponse = JsonMapper.ToObject(jsonResponse);
                            _secrets.id = (string)objectResponse["data"][0]["id"];
                            _secrets.username = (string)objectResponse["data"][0]["login"];
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        pageData = "<!DOCTYPE><html><head></head><body><b>DONE!</b><br>(Please close this tab/window)</body></html>";
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
        }

        public async void Update()
        {
            if (_secrets?.IsValid() ?? false)
            {
                if (!pubsub?.IsClientConnected() ?? true)
                {
                    await StartTwitchPubSubClient();
                    return;
                }

                if (!pubsub.IsChannelConnected(_secrets.username, out PubSubChannel))
                {
                    PubSubChannel = pubsub.JoinChannel(_secrets.username);
                    PubSubChannel.MessageReceived += eventManager.PubSubMessageReceived;
                    return;
                }

                if (!client?.IsClientConnected() ?? true)
                {
                    await StartTwitchChatClient();
                    return;
                }

                if(!client.IsChannelConnected(_secrets.username, out TextChannel))
                {
                    TextChannel = await client.JoinChannelAsync(_secrets.username, cts);
                    TextChannel.MessageReceived += eventManager.ChatMessageReceived;
                    await TextChannel.SendMessageAsync($"ModBot Connected.", cts);
                    return;
                }

                timer.Update();
            }
        }

        public async Task StartTwitchChatClient()
        {
            while(!loggedIn)
                await Task.Yield();

            if(!_secrets?.IsValid() ?? true)
                return;
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
                    await TextChannel.SendMessageAsync($"{eventLookup.GetAll()}", cts);
                    Console.WriteLine("ModBot Connected");
                    Console.WriteLine(eventLookup.GetAll());
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
                await StartTwitchChatClient();
            }
        }

        public async Task StopTwitchChatClient()
        {
            while (!loggedIn)
                await Task.Yield();

            if (!_secrets?.IsValid() ?? true)
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

        public async Task StartTwitchPubSubClient()
        {
            while (!loggedIn)
                await Task.Yield();

            if (!_secrets?.IsValid() ?? true)
                return;
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
            catch(Exception e)
            {
                Console.WriteLine(e);
                await StartTwitchPubSubClient();
            }
        }

        public async Task StopTwitchPubSubClient()
        {
            while (!loggedIn)
                await Task.Yield();

            if (!_secrets?.IsValid() ?? true)
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