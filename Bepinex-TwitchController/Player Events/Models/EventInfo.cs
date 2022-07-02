namespace TwitchController.Player_Events.Models
{
    using System;

    public class EventInfo
    {
        public string Perp;
        public Action<string, string> Action;
        public int BitCost;
        public int CooldownSeconds;

        public EventInfo(Action<string, string> action, int bitCost, int cooldownSeconds)
        {
            Action = action;
            BitCost = bitCost;
            CooldownSeconds = cooldownSeconds;
        }

        public EventInfo(string perp, EventInfo eventInfo)
        {
            Perp = perp;
            Action = eventInfo.Action;
            BitCost = eventInfo.BitCost;
            CooldownSeconds = eventInfo.CooldownSeconds;
        }
    }
}
