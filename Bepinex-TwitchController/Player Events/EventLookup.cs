namespace TwitchController.Player_Events
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TwitchController.Player_Events.Models;

    public class EventLookup
    {
        private readonly Controller controller;

        public EventLookup(Controller twitchController)
        {
            controller = twitchController;
        }

        // Queue for events
        public List<KeyValuePair<string, EventInfo>> ActionQueue = new List<KeyValuePair<string, EventInfo>>();

        // Queue for the cleanup code of timed events
        public List<Action> TimedActionsQueue = new List<Action>();

        // List with currently running timed events
        public List<string> RunningEventIDs = new List<string>();

        // List with currently running timed events
        public Dictionary<string, DateTime> Cooldowns = new Dictionary<string, DateTime>();

        //MAP OF EVENTS TO THEIR APPROPRIATE FUNCTIONS
        // Parameter: ID, Action<string, string>, BitCost, CooldownSeconds
        internal readonly Dictionary<string, EventInfo> EventDictionary = new Dictionary<string, EventInfo>();

        public void RemoveEvent(string EventID)
        {
            if (EventDictionary.Keys.Contains(EventID))
            {
                EventDictionary.Remove(EventID);
            }
        }

        public void ChangeCost(string EventID, int newCost)
        {
            if (EventDictionary.TryGetValue(EventID, out var eventInfo))
            {
                eventInfo.BitCost = newCost;
                EventDictionary[EventID] = eventInfo;
            }
        }

        public bool AddEvent(string EventID, ref EventInfo eventInfo)
        {
            if (!EventDictionary.Keys.Contains(EventID))
            {
                EventDictionary.Add(EventID, eventInfo);
                return true;
            }
            Console.WriteLine($"[Error] Event with ID: {EventID} already registered!");
            return false;
        }

        public bool AddEvent(string EventID, ref DataEventInfo eventInfo)
        {
            if (!EventDictionary.Keys.Contains(EventID))
            {
                EventDictionary.Add(EventID, eventInfo);
                return true;
            }
            Console.WriteLine($"[Error] Event with ID: {EventID} already registered!");
            return false;
        }

        public bool AddEvent(string EventID, Action<string, string> Action, int BitCost, int CooldownSeconds)
        {
            if (!EventDictionary.Keys.Contains(EventID))
            {
                EventDictionary.Add(EventID, new EventInfo(Action, BitCost, CooldownSeconds));
                return true;
            }
            Console.WriteLine($"[Error] Event with ID: {EventID} already registered!");
            return false;
        }

        public bool AddTimedEvent(string EventID, Action<string, string> Action, int BitCost, int CooldownSeconds, Action TimedAction, int TimerLength)
        {
            if (!EventDictionary.Keys.Contains(EventID))
            {
                EventDictionary.Add(EventID, new TimedEventInfo(Action, BitCost, CooldownSeconds, TimedAction, TimerLength));
                return true;
            }
            Console.WriteLine($"[Error] Event with ID: {EventID} already registered!");
            return false;
        }

        public bool TryGetEvent(string EventID, out EventInfo eventInfo)
        {
            return EventDictionary.TryGetValue(EventID, out eventInfo);
        }

        public bool Contains(string EventID)
        {
            return EventDictionary.ContainsKey(EventID);
        }

        public async void SendBitsEvents()
        {
            foreach (KeyValuePair<string, EventInfo> pair in EventDictionary.OrderBy(p => p.Value.BitCost).ThenBy(p => p.Key))
            {
                if (pair.Value.BitCost > 0)
                {
                    string costText = $"[ {pair.Key} ]: {pair.Value.BitCost} bits";
                    await controller.TextChannel.SendMessageAsync(costText, controller.cts);
                }
            }
        }

        public async void SendAllEvents()
        {
            foreach (KeyValuePair<string, EventInfo> pair in EventDictionary.OrderBy(p=>p.Value.BitCost).ThenBy(p=>p.Key))
            {
                await controller.TextChannel.SendMessageAsync($"[ {pair.Key} ]{(pair.Value.BitCost > 0 ? $": {pair.Value.BitCost} bits" : "")}", controller.cts);
            }
        }

        public void Lookup(string EventText, string perp, Message message = null)
        {
            if (EventDictionary.TryGetValue(EventText.Trim(), out EventInfo eventInfo))
            {
                switch (eventInfo)
                {
                    case TimedEventInfo timed:
                        TimedEventInfo tei = new TimedEventInfo(perp, timed);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(EventText, tei));
                        break;
                    case DataEventInfo dataEventInfo when message?.Data != null:
                        DataEventInfo info = new DataEventInfo(perp, dataEventInfo, message.Data);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(EventText, info));
                            break;
                    default:
                        EventInfo ei = new EventInfo(perp, eventInfo);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(EventText, ei));
                        break;
                }
            }
        }

        public void Lookup(int bits, string perp, Message message = null)
        {
            var Events = EventDictionary.Where(it => it.Value.BitCost > 0 && it.Value.BitCost == bits)?.ToList() ?? new List<KeyValuePair<string, EventInfo>>();
            KeyValuePair<string, EventInfo> Event = default(KeyValuePair<string, EventInfo>);
            if(Events.Count > 0)
                Event = Events[new Random().Next(0, Events.Count - 1)];
            else
                Event = EventDictionary.Where(it => it.Value.BitCost > 0 && it.Value.BitCost <= bits)?.OrderByDescending(it => it.Value.BitCost)?.FirstOrDefault() ?? default;

            if (!Event.Equals(default(KeyValuePair<string, EventInfo>)))
            {
                switch(Event.Value)
                {
                    case TimedEventInfo timed:
                        TimedEventInfo tei = new TimedEventInfo(perp, timed);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(Event.Key, tei));
                        controller.timer.AddQueueEvent(Event.Key);
                        break;
                    case DataEventInfo dataEventInfo when message?.Data != null:
                        DataEventInfo info = new DataEventInfo(perp, dataEventInfo, message.Data);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(Event.Key, info));
                        break;
                    default:
                        EventInfo ei = new EventInfo(perp, Event.Value);
                        ActionQueue.Add(new KeyValuePair<string, EventInfo>(Event.Key, ei));
                        controller.timer.AddQueueEvent(Event.Key);
                        break;
                }
            }
        }

    }
}
