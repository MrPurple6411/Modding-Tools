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

            if (triggerLower == "!allevents")
            {
                controller.eventLookup.SendAllEvents();
                return;
            }

            if (triggerLower == "!events")
            {
                controller.eventLookup.SendBitsEvents();
                return;
            }

            string username = e.User.ToLower().Trim();
            string streamer = Controller._secrets.username.ToLower().Trim();
            string bot = Controller._secrets.botname.ToLower().Trim();
            List<string> mods = Controller._secrets.authorizedModerators;

            if (username == streamer || username == bot || mods.Contains(username))
            {
                string user;
                int bits;

                if (e.TriggerText.StartsWith("!"))
                {
                    string[] x = e.TriggerText.Split('/');

                    if (x.Length == 2)
                    {
                        user = x[0].Substring(1);
                        string trigger = x[1].Trim();
                        if (!int.TryParse(trigger, out bits))
                        {
                            if (controller.eventLookup.Contains(trigger))
                            {
                                Console.WriteLine($"User:{user},  Trigger:{trigger}");
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
                            Console.WriteLine($"User:{user},  Bits:{bits}");
                            controller.eventLookup.Lookup(bits, user, e);
                            return;
                        }
                    }
                }

                if (e.TriggerText.Contains("WE DID IT! WE HIT A LEVEL 5 HYPE TRAIN!"))
                {
                    controller.eventLookup.Lookup("HypeTrainLevel5Complete", "HypeTrain 5 Complete!!!", e);
                    return;
                }

                Regex regex = new Regex(Controller._secrets.regex);
                Match match = regex.Match(e.TriggerText);

                Regex not_num_period = new Regex("[^0-9.]");


                if (!match.Success)
                    return;

                user = match.Groups["user"].Value;
                string donation = not_num_period.Replace(match.Groups["donation"].Value, "");

                if (!float.TryParse(donation, out float donated))
                {
                    Console.WriteLine($"Parsing tip as float failed for {user} and amount of {donation}");
                    return;
                }

                bits = (int)(donated * 100);

                Console.WriteLine($"User:{user},  Bits:{bits}");
                controller.eventLookup.Lookup(bits, user, e);
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
