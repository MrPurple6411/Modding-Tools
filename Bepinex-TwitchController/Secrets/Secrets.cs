namespace TwitchController
{
    using LitJson;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;

    public class Secrets
    {

        private readonly string  _configFilePath = Path.Combine(Path.GetDirectoryName(Environment.CurrentDirectory), "/TwitchConfig.json");
        private string ConfigFilePath => _configFilePath;
        private readonly Config config;

        protected internal string client_id = "zt8acgpm7mzey3eqi0sro0ozc1fqvz";
        protected internal string access_token;
        protected internal string id;
        protected internal string username;
        protected internal string botname;
        protected internal string regex;

        public Secrets(string configFilePath)
        {
            _configFilePath = configFilePath;
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    config = JsonMapper.ToObject<Config>(File.ReadAllText(ConfigFilePath)); 
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    config = new Config();
                }
            }
            else
            {
                config = new Config();
                StringBuilder stringBuilder = new StringBuilder();
                JsonMapper.ToJson(config, new JsonWriter(stringBuilder) { PrettyPrint = true });

                File.WriteAllText(ConfigFilePath, stringBuilder.ToString());

            }
            
            botname = config.BotName;
            regex = config.TipsRegEx;

            StringBuilder stringBuilder2 = new StringBuilder();
            JsonMapper.ToJson(config, new JsonWriter(stringBuilder2) { PrettyPrint = true });
            File.WriteAllText(ConfigFilePath, stringBuilder2.ToString());

        }

        public bool IsValid()
        {
            return access_token != null && id != null && username != null;
        }
    }
}