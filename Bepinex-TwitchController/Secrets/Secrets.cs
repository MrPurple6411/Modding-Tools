﻿namespace TwitchController
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;

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
        internal List<string> authorizedModerators = new List<string>();

        public Secrets(string configFilePath)
        {
            _configFilePath = configFilePath;
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigFilePath)); 
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
            }
            
            botname = config.BotName;
            regex = config.TipsRegEx;
            foreach(var authorizedModerator in config.AuthorizedModerators)
            {
                authorizedModerators.Add(authorizedModerator.ToLower().Trim());
            }

            File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public bool IsValid()
        {
            return access_token != null && id != null && username != null;
        }
    }
}