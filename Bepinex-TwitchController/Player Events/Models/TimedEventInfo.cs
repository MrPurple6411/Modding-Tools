namespace TwitchController.Player_Events.Models
{
    using System;

    /// <summary>
    /// TimedEvents trigger an action at start and then another action after the timer has completed.
    /// </summary>
    public class TimedEventInfo : EventInfo
    {
        public Action TimedAction;
        public int TimerLength;

        public TimedEventInfo(Action<string, string> action, int bitCost, int cooldownSeconds, Action timedAction, int timerLength) : base(action, bitCost, cooldownSeconds)
        {
            TimedAction = timedAction;
            TimerLength = timerLength;
        }

        public TimedEventInfo(string perp, TimedEventInfo timedEventInfo) : base(perp, timedEventInfo)
        {

            TimedAction = timedEventInfo.TimedAction;
            TimerLength = timedEventInfo.TimerLength;
        }
    }
}
