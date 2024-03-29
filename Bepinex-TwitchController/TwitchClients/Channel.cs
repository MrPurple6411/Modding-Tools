﻿namespace TwitchController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Channel
    {
        public string Name { get; private set; }

        public event EventHandler<Message> MessageReceived;

        private readonly IMessageClient _messageClient;

        internal Channel(string channelName, IMessageClient messageClient)
        {
            Name = channelName;
            _messageClient = messageClient;
        }

        /// <summary>
        /// Sends a private message to a channel.
        /// </summary>
        /// <param name="channel">A channel name.</param>
        /// <param name="message">Message to be send. Limited to 512 bytes.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            return _messageClient.SendMessageAsync($"PRIVMSG #{Name} :{message}", cancellationToken);
        }

        /// <summary>
        /// Departs from a channel.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task LeaveChannelAsync(CancellationToken cancellationToken)
        {
            return _messageClient.SendMessageAsync($"PART #{Name.ToLower()}", cancellationToken);
        }

        internal void ReceiveMessage(Message message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}