namespace TwitchController
{
    internal class Config
    {
        public string BotName { get; set; } = "Streamlabs";

        public string TipsRegEx { get; set; } = "(?<user>.*) just tipped (?<donation>.*)!";

    }

}
