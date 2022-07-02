namespace TwitchController
{
    using System.Collections.Generic;

    internal class Config
    {
        public string BotName { get; set; } = "Streamlabs";

        public string TipsRegEx { get; set; } = "(?<user>.*) just tipped (?<donation>.*)!";

        public List<string> AuthorizedModerators = new List<string>() { "MrPurple6411" };

    }

}
