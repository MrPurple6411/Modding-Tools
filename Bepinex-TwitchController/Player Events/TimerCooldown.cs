namespace TwitchController
{
    using System;
    using System.Collections.Generic;
    using TwitchController.Player_Events.Models;

    internal class TimerCooldown
    {

        private readonly Controller controller;

        private readonly List<CustomEvent> customTimerEvents;

        private readonly List<KeyValuePair<string, CustomEvent>> actionQueueEvents;
        private readonly List<KeyValuePair<string, CustomEvent>> cooldownEvents;

        internal TimerCooldown(Controller twitchController)
        {
            controller = twitchController;
            customTimerEvents = new List<CustomEvent>();
            actionQueueEvents = new List<KeyValuePair<string, CustomEvent>>();
            cooldownEvents = new List<KeyValuePair<string, CustomEvent>>();

            controller.eventLookup.ActionQueue = new List<KeyValuePair<string, EventInfo>>();
            controller.eventLookup.Cooldowns = new Dictionary<string, DateTime>();
            controller.eventLookup.RunningEventIDs = new List<string>();
            controller.eventLookup.TimedActionsQueue = new List<Action>();
        }

        internal void AddCooldown(string text, float duration, EventInfo eventInfo)
        {
            CustomEvent cooldownText = new CustomEvent(duration);
            cooldownText.SetEvent(new KeyValuePair<string, EventInfo>(text, eventInfo));
            customTimerEvents.Add(cooldownText);
        }

        internal void AddCooldown(string text, TimedEventInfo timedEvent)
        {
            CustomEvent cooldownText = new CustomEvent(timedEvent.TimerLength);
            cooldownText.SetTimedEvent(new KeyValuePair<string, TimedEventInfo>(text, timedEvent));
            controller.eventLookup.RunningEventIDs.Add(text);
            customTimerEvents.Add(cooldownText);
        }

        internal void Update()
        {
            for (int i = 0; i < customTimerEvents.Count; i++)
            {
                CustomEvent customEvent = customTimerEvents[i];

                if (customEvent.IsFinished())
                {
                    if (customEvent.HasTimedEvent())
                    {
                        KeyValuePair<string, TimedEventInfo> timedEvent = customEvent.GetTimedEvent();
                        controller.eventLookup.RunningEventIDs.Remove(timedEvent.Key);
                        controller.eventLookup.TimedActionsQueue.Add(timedEvent.Value.TimedAction);
                        controller.eventLookup.Cooldowns.Add(timedEvent.Key, DateTime.Now);
                        AddCooldownEvent(timedEvent.Key, timedEvent.Value.CooldownSeconds, timedEvent.Value);
                    }
                    customTimerEvents.RemoveAt(i);
                    i--;
                }
            }

            List<string> finishedCooldowns = new List<string>();

            foreach (KeyValuePair<string, CustomEvent> cooldownText in cooldownEvents)
            {
                KeyValuePair<string, EventInfo> eventInfo = cooldownText.Value.GetEvent();
                double currentCooldownDuration = (DateTime.Now - controller.eventLookup.Cooldowns[eventInfo.Key]).TotalSeconds;
                if (currentCooldownDuration >= eventInfo.Value.CooldownSeconds)
                {
                    finishedCooldowns.Add(eventInfo.Key);
                }
            }

            foreach (string finishedCooldown in finishedCooldowns)
            {
                RemoveCooldownEvent(finishedCooldown);
                controller.eventLookup.Cooldowns.Remove(finishedCooldown);
            }


            while (controller.eventLookup.TimedActionsQueue.Count > 0)
            {
                try
                {
                    // Execute all the timed event cleanup code BEFORE any of the other events
                    Action action = controller.eventLookup.TimedActionsQueue[0];
                    controller.eventLookup.TimedActionsQueue.RemoveAt(0);

                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Error] Failed to invoke action " + e.Message);
                    Console.WriteLine(e.StackTrace);
                    controller.eventLookup.TimedActionsQueue.RemoveAt(0);
                }
            }

            if (controller.eventLookup.ActionQueue.Count > 0)
            {
                KeyValuePair<string, EventInfo> localEventInfo = new KeyValuePair<string, EventInfo>();
                int eventIndex = -1;

                for (int i = 0; i < controller.eventLookup.ActionQueue.Count; i++)
                {
                    KeyValuePair<string, EventInfo> keyValuePair = controller.eventLookup.ActionQueue[i];
                    if (controller.eventLookup.RunningEventIDs.Contains(keyValuePair.Key) || controller.eventLookup.Cooldowns.ContainsKey(keyValuePair.Key))
                    {
                        continue;
                    }
                    // Safe to use, doesnt have cooldown / is currently in use
                    localEventInfo = keyValuePair;
                    eventIndex = i;
                }

                if (eventIndex >= 0)
                {
                    controller.eventLookup.ActionQueue.RemoveAt(eventIndex);
                    RemoveQueueEvent(localEventInfo.Key);
                    if (localEventInfo.Value is TimedEventInfo timedEventInfo)
                    {
                        // If its a timed event, pass that info to the TimerCooldown
                        AddCooldown(localEventInfo.Key, timedEventInfo);
                    }
                    else
                    {
                        AddCooldown(localEventInfo.Key, 1, localEventInfo.Value);
                        controller.eventLookup.Cooldowns.Add(localEventInfo.Key, DateTime.Now);
                        AddCooldownEvent(localEventInfo.Key, localEventInfo.Value.CooldownSeconds, localEventInfo.Value);
                    }

                    if (localEventInfo.Value is DataEvent dataEvent)
                    {

                        try
                        {
                            dataEvent.DataAction?.Invoke(localEventInfo.Key, localEventInfo.Value.Perp, dataEvent.UserInput);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[Error] Failed to invoke action " + e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }

                    try
                    {
                        localEventInfo.Value.Action?.Invoke(localEventInfo.Key, localEventInfo.Value.Perp);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[Error] Failed to invoke action " + e.Message);
                        Console.WriteLine(e.StackTrace);
                    }
                }

            }
        }

        internal void AddCooldownEvent(string text, float duration, EventInfo eventInfo)
        {
            CustomEvent cooldownText = new CustomEvent(duration);
            cooldownText.SetEvent(new KeyValuePair<string, EventInfo>(text, eventInfo));
            cooldownEvents.Add(new KeyValuePair<string, CustomEvent>(text, cooldownText));
        }

        internal void AddQueueEvent(string text)
        {
            CustomEvent cooldownText = new CustomEvent(float.MaxValue);
            actionQueueEvents.Add(new KeyValuePair<string, CustomEvent>(text, cooldownText));
        }

        internal void RemoveCooldownEvent(string text)
        {
            for (int i = 0; i < cooldownEvents.Count; i++)
            {
                KeyValuePair<string, CustomEvent> keyValuePair = cooldownEvents[i];
                if (text.Equals(keyValuePair.Key))
                {
                    // Only remove once, because it should never be in here multiple times
                    cooldownEvents.RemoveAt(i);
                    break;
                }
            }
        }

        internal void RemoveQueueEvent(string text)
        {
            for (int i = 0; i < actionQueueEvents.Count; i++)
            {
                KeyValuePair<string, CustomEvent> keyValuePair = actionQueueEvents[i];
                if (text.Equals(keyValuePair.Key))
                {
                    // Only remove once, because if its in here multiple times, then multiple people have submitted it
                    actionQueueEvents.RemoveAt(i);
                    break;
                }
            }
        }

        private class CustomEvent
        {
            private readonly float duration;
            private readonly DateTime startTime;

            private KeyValuePair<string, TimedEventInfo> timedEvent;
            private KeyValuePair<string, EventInfo> normalEvent;
            private bool hasTimedEvent = false;

            public CustomEvent(float duration)
            {
                this.duration = duration;
                startTime = DateTime.Now;

            }

            public bool IsFinished()
            {
                return (DateTime.Now - startTime).TotalSeconds >= duration;
            }

            public void SetTimedEvent(KeyValuePair<string, TimedEventInfo> timedEvent)
            {
                this.timedEvent = timedEvent;
                hasTimedEvent = true;
            }

            public KeyValuePair<string, TimedEventInfo> GetTimedEvent()
            {
                return timedEvent;
            }

            public void SetEvent(KeyValuePair<string, EventInfo> normalEvent)
            {
                this.normalEvent = normalEvent;
            }

            public KeyValuePair<string, EventInfo> GetEvent()
            {
                return normalEvent;
            }

            public bool HasTimedEvent()
            {
                return hasTimedEvent;
            }

        }

    }
}
