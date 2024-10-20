namespace TwitchController
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using TwitchController.Player_Events.Models;

    internal class TwitchEventManager
    {
        private readonly Controller controller;

        public TwitchEventManager(Controller twitchController)
        {
            controller = twitchController;
        }

        public void ChatMessageReceived(object _, Message e)
        {
            string triggerLower = e.TriggerText.ToLower().Trim();
            string username = e.User.Trim();
            string streamer = Controller._secrets.username.Trim();
            string bot = Controller._secrets.botname.Trim();
            bool authorized = username == streamer || username == bot || Controller._secrets.authorizedModerators.Contains(username);

            if (triggerLower == "!events")
            {
                controller.eventLookup.SendBitsEvents();
                return;
            }

            if (!authorized)
            {
                return;
            }

            if (triggerLower == "!allevents")
            {
                controller.eventLookup.SendAllEvents();
                return;
            }

            if (e.TriggerText.Contains("WE DID IT! WE HIT A LEVEL 5 HYPE TRAIN!"))
            {
                controller.eventLookup.Lookup("HypeTrainLevel5Complete", "HypeTrain 5 Complete!!!", e);
                return;
            }

            Regex regex = new Regex(Controller._secrets.regex);
            Match match = regex.Match(e.TriggerText);
            Regex not_num_period = new Regex("[^0-9.]");


            if (match.Success)
            {
                string user = match.Groups["user"].Value;
                string donation = not_num_period.Replace(match.Groups["donation"].Value, "");

                if (string.IsNullOrWhiteSpace(donation) || !float.TryParse(donation, out float donated))
                {
                    Console.WriteLine($"Parsing tip as float failed for {user} and amount of {donation}");
                    return;
                }

                int tip = (int)(donated * 100);

                Console.WriteLine($"User:{user},  Tip:{tip}");
                controller.eventLookup.Lookup(tip, user, e);
                return;
            }

            if (e.TriggerText.StartsWith("!"))
            {
                string[] x = e.TriggerText.Trim().Split('/');

                switch (x.Length)
                {
                    case 1:
                        {
                            // if first letter after the ! is e, it's a named event and if it's b, it's bits
                            string type = triggerLower.Trim().Substring(1, 1);
                            string trigger = e.TriggerText.Trim().Substring(2);
                            if (string.IsNullOrWhiteSpace(trigger))
                            {
                                // trigger !e is invalid as it needs an event name
                                Console.WriteLine($"[Error] Trigger {e.TriggerText.Trim()} is invalid as it needs an event name eg !e1000, !b1000, !ePrime, !e1000, !$5.00");
                            }

                            switch (type)
                            {
                                case "e" when controller.eventLookup.Contains(trigger):
                                    Console.WriteLine($"User:{streamer},  Trigger:{trigger}");
                                    controller.eventLookup.Lookup(trigger, username, e);
                                    return;
                                case "b" when int.TryParse(trigger, out int bits):
                                    Console.WriteLine($"User:{username},  Bits:{bits}");
                                    controller.eventLookup.Lookup(bits, username, e);
                                    return;
                                case "$" when (float.TryParse(trigger, out float donated)):
                                    int tip = (int)(donated * 100);
                                    Console.WriteLine($"User:{username},  Tip:{tip}");
                                    controller.eventLookup.Lookup(tip, username, e);
                                    return;
                                default:
                                    Console.WriteLine($"[Error] Trigger {e.TriggerText.Trim()} is invalid.");
                                    break;
                            }
                            break;
                        }
                    case 2:
                        {
                            string user = x[0].Substring(1);
                            string trigger = x[1].Trim();

                            if (string.IsNullOrWhiteSpace(trigger))
                            {
                                // needs something after the / eg !MrPurple6411/Prime or !MrPurple6411/100
                                Console.WriteLine($"[Error] Trigger {e.TriggerText.Trim()} is invalid as it needs something after the / eg !MrPurple6411/Prime or !MrPurple6411/100");
                            }

                            if (!int.TryParse(trigger, out int bits))
                            {
                                if (controller.eventLookup.Contains(trigger))
                                {
                                    Console.WriteLine($"User:{user},  Length: {x.Length},  Trigger:{trigger}");
                                    controller.eventLookup.Lookup(trigger, user, e);
                                    return;
                                }
                                else
                                {
                                    Console.WriteLine($"{trigger} Not Found");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"User:{user},  Length: {x.Length},  Bits:{bits}");
                                controller.eventLookup.Lookup(bits, user, e);
                                return;
                            }

                            break;
                        }
                    case 3:
                        {
                            string user = x[0].Substring(1);
                            string type = x[1].Trim().ToLowerInvariant();
                            string trigger = x[2].Trim();

                            if (string.IsNullOrWhiteSpace(trigger))
                            {
                                // needs something after the / eg !MrPurple6411/event/Prime or !MrPurple6411/bits/100
                                Console.WriteLine($"[Error] Trigger {e.TriggerText.Trim()} is invalid as it needs something after the / eg !MrPurple6411/Prime or !MrPurple6411/100");
                            }

                            switch (type)
                            {
                                case "event":
                                case "e":
                                    if (controller.eventLookup.Contains(trigger))
                                    {
                                        Console.WriteLine($"User:{user},  Length: {x.Length},  Trigger:{trigger}");
                                        controller.eventLookup.Lookup(trigger, user, e);
                                        return;
                                    }
                                    else
                                    {
                                        Console.WriteLine($"{trigger} Not Found");
                                    }
                                    break;
                                case "bits":
                                case "b":
                                    {
                                        int bits;
                                        if (!int.TryParse(trigger, out bits))
                                        {
                                            Console.WriteLine($"failed to parse {trigger} in {e.TriggerText} to a bits value.");
                                            return;
                                        }
                                        Console.WriteLine($"User:{user},  Length: {x.Length},  Bits:{bits}");
                                        controller.eventLookup.Lookup(bits, user, e);
                                        return;
                                    }

                                default:
                                    Console.WriteLine($"Invalid type {type} in {e.TriggerText}. Must be 'event', 'e', 'bits' or 'b'");
                                    return;
                            }

                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"Invalid number of arguments in {e.TriggerText}");
                            return;
                        }
                }
            }

        }

        public void PubSubMessageReceived(object _, Message e)
        {
            if (e.Host == ChannelPointsHost() || e.Host == SubscriptionHost())
            {
                Console.WriteLine($"Host:{e.Host},  User:{e.User},  Trigger:{e.TriggerText}");
                controller.eventLookup.Lookup(e.TriggerText, e.User, e);
                return;
            }

            if (e.Host == BitsHost())
            {
                Console.WriteLine($"{e.Host}: {e.User}, {e.TriggerText}");
                controller.eventLookup.Lookup(int.Parse(e.TriggerText), e.User, e);
                return;
            }
        }

        private string ChannelPointsHost()
        {
            return "channel-points-channel-v1." + Controller._secrets.id;
        }

        private string BitsHost()
        {
            return "channel-bits-events-v2." + Controller._secrets.id;
        }

        private string SubscriptionHost()
        {
            return "channel-subscribe-events-v1." + Controller._secrets.id;
        }

    }
}
