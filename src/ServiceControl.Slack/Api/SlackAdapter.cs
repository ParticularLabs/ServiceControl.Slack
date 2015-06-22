namespace ServiceControl.Slack.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;
    using Serilog;
    using ServiceStack;
    using ServiceStack.Text;
    using SuperSocket.ClientEngine;
    using WebSocket4Net;

    public class SlackAdapter
    {
        private readonly Subject<Message> messages;
        private readonly string token;

        private SlackAPI api;
        private WebSocket socket;
        private bool reconnect;
        private List<User> users;
        private List<Im> ims;
        private List<Channel> channels;

        public SlackAdapter(string token)
        {
            this.token = token;
            messages = new Subject<Message>();
            Messages = messages.AsObservable();
        }

        public string BotUserId { get; private set; }

        public Task Start()
        {
            reconnect = true;
            if (!string.IsNullOrEmpty(token) && api == null)
            {
                api = new SlackAPI(token);
            }

            return ConnectSocket();
        }

        public Task Stop()
        {
            reconnect = false;
            if (socket != null)
            {
                socket.Close();
            }
            return Task.FromResult(0);
        }

        public Task Send(string destination, string message)
        {
            if (socket == null || socket.State != WebSocketState.Open)
            {
                Log.Error("Socket not open so cannot send message '{message}' to {destination}.", message, destination);
                return Task.FromResult(0);
            }

            if (destination == null)
                return Task.FromResult(0);

            var room = destination;

            var channel = channels.FirstOrDefault(r => StringComparer.InvariantCultureIgnoreCase.Equals(r.Id, room) ||
                StringComparer.InvariantCultureIgnoreCase.Equals(r.Name, room));

            if (channel != null)
            {
                room = channel.Id;
                //EnsureBotInRoom(channel);

                if (!channel.IsMember)
                {
                    // Currently bots cannot self enter a room.
                    // Instead we'll just log for now.
                    Log.Error("Bots cannot join rooms. Invite bot into room {name}({id})", channel.Name, channel.Id);
                    return Task.FromResult(0);
                }
            }

            var im = ims.FirstOrDefault(i => StringComparer.InvariantCultureIgnoreCase.Equals(i.Id, room) ||
                StringComparer.InvariantCultureIgnoreCase.Equals(i.User, room));

            if (im != null)
            {
                room = im.Id;
                EnsureBotInRoom(im);
            }

            var user = users.FirstOrDefault(u => StringComparer.InvariantCultureIgnoreCase.Equals(u.Id, room) ||
                StringComparer.InvariantCultureIgnoreCase.Equals(u.Name, room));

            if (user != null)
            {
                var response = api.ImOpen(user.Id);
                if (!response.Ok)
                {
                    Log.Error("Could not join im channel {id} ({error})", user.Id, response.Error);
                    return Task.FromResult(0);
                }
                room = response.Channel;
                im = new Im() { Id = response.Channel, User = user.Id, IsOpen = true };
                ims.Add(im);
            }

            Log.Debug("Sending message {message} to {destination}", message, destination);

            SlackAPI.Send(socket, room, message);

            return Task.FromResult(0);
        }

        public IObservable<Message> Messages { get; private set; }

        private async Task ConnectSocket()
        {
            if (api == null)
            {
                return;
            }

            for (var i = 0; i < 10 && socket == null; i++)
            {
                var start = api.RtmStart();
                if (!start.Ok)
                {
                    if (start.Error == "invalid_auth")
                    {
                        Log.Error("Invalid bot token.");
                        return;
                    }

                    Log.Warning("Unable to start connection with Slack Adapter. Retrying");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }

                // URL only valid for 30sec
                socket = new WebSocket(start.Url);

                BotUserId = start.Self.Id;

                users = start.Users.ToList();
                channels = start.Channels.ToList();
                ims = start.Ims.ToList();
            }

            if (socket == null)
            {
                Log.Error("Unable to create socket for Slack Adapter");
                return;
            }

            socket.Closed += SocketOnClose;
            socket.Error += SocketOnError;
            socket.MessageReceived += SocketOnMessage;

            var openingTcs = new TaskCompletionSource<object>();
            socket.Opened += (s, e) => openingTcs.SetResult(null);
            socket.Open();
            await openingTcs.Task;

            Log.Information("Slack socket connected");
        }

        private void SocketOnError(object sender, ErrorEventArgs errorEventArgs)
        {
            Log.Error(errorEventArgs.Exception, "Slack Socket Error - {message}", errorEventArgs.Exception.Message);
        }

        private async void SocketOnClose(object sender, EventArgs closeEventArgs)
        {
            socket.Close();
            socket = null;

            while (reconnect && socket == null)
            {
                await ConnectSocket();

                if (socket == null)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        private void SocketOnMessage(object sender, MessageReceivedEventArgs messageEventArgs)
        {
            var raw = JsonObject.Parse(messageEventArgs.Message);

            if (raw["type"] == "message")
            {
                if (raw.ContainsKey("subtype"))
                {
                    // Subtypes are things like channel join messages, etc.
                    return;
                }

                var channel = raw["channel"];
                var text = raw["text"];
                var user = raw["user"];

                ReceiveMessage(channel, user, text);
            }

            if (raw["type"] == "team_join")
            {
                HandleTeamJoinMessage(raw);
            }

            if (raw["type"] == "channel_created")
            {
                HandleChannelCreatedMessage(raw);
            }

            if (raw["type"] == "channel_deleted")
            {
                HandleChannelDeletedMessage(raw);
            }

            if (raw["type"] == "channel_rename")
            {
                HandleChannelRenameMessage(raw);
            }
        }

        private void HandleTeamJoinMessage(JsonObject raw)
        {
            var newUser = raw.GetUnescaped("user").FromJson<User>();

            Log.Debug("User {name}({id}) joined the team", newUser.Name, newUser.Id);

            if (!newUser.IsBot)
            {
                users.Add(newUser);
            }
        }

        private void HandleChannelCreatedMessage(JsonObject raw)
        {
            var channel = raw.GetUnescaped("channel").FromJson<Channel>();

            Log.Debug("Channel {name}({id}) created", channel.Name, channel.Id);

            AddRoom(channel);
        }

        private void HandleChannelDeletedMessage(JsonObject raw)
        {
            var id = raw["channel"];
            var channel = channels.FirstOrDefault(c => StringComparer.InvariantCultureIgnoreCase.Equals(c.Id, id));
            if (channel == null)
            {
                return;
            }
            var name = channel.Name;

            Log.Debug("Channel {name}({id}) deleted", name, id);

            RemoveRoom(channel);
        }

        private void HandleChannelRenameMessage(JsonObject raw)
        {
            var channel = raw.GetUnescaped("channel").FromJson<Channel>();
            var oldChannel = channels.FirstOrDefault(c => StringComparer.InvariantCultureIgnoreCase.Equals(c.Id, channel.Id));
            if (oldChannel == null)
            {
                return;
            }
            var oldName = oldChannel.Name;

            Log.Debug("Channel {id} renamed from {oldname} to {newname}", channel.Id, oldName, channel.Name);

            if (!string.IsNullOrEmpty(oldName))
            {
                RemoveRoom(oldChannel);
            }

            AddRoom(channel);
        }

        private void ReceiveMessage(string channelId, string userId, string text)
        {
            if (userId == BotUserId)
            {
                // Don't respond to self
                return;
            }

            var user = users.FirstOrDefault(u => StringComparer.InvariantCultureIgnoreCase.Equals(u.Id, userId));
            if (user == null)
            {
                // Message probably came from an integration. Move on
                return;
            }

            var im = ims.FirstOrDefault(i => i.User == userId);

            var envelope = new Envelope(userId, im != null ? im.Id : "", channelId, ims.Any(i => i.Id == channelId) ? EnvelopeType.Im : EnvelopeType.Channel);

            var message = new Message(envelope, text.Trim());

            messages.OnNext(message);
        }

        private void AddRoom(Channel channel)
        {
            channels.Add(channel);
        }

        private void RemoveRoom(Channel channel)
        {
            channels.Remove(channel);
        }

        private void EnsureBotInRoom(Channel channel)
        {
            if (channel.IsMember)
                return;

            var response = api.ChannelsJoin(channel.Name);

            if (!response.Ok)
            {
                Log.Error("Could not join channel {name} ({error})", channel.Name, response.Error);
                return;
            }

            channel.IsMember = true;
        }

        private void EnsureBotInRoom(Im directMessage)
        {
            if (directMessage.IsOpen)
                return;

            var response = api.ImOpen(directMessage.User);

            if (!response.Ok)
            {
                Log.Error("Could not join im channel {name} ({error})", directMessage.User, response.Error);
                return;
            }

            directMessage.IsOpen = true;
        }

        //public UserInfo GetUserInfo(string userId)
        //{
        //    var user = users.FirstOrDefault(u => StringComparer.InvariantCultureIgnoreCase.Equals(u.Id, userId));

        //    if (user == null)
        //        return null;

        //    return new UserInfo()
        //    {
        //        Id = user.Id,
        //        Name = user.Name,
        //        RealName = user.RealName
        //    };
        //}

        //public string GetUserToken(Envelope envelope)
        //{
        //    var user = users.FirstOrDefault(u => StringComparer.InvariantCultureIgnoreCase.Equals(u.Id, envelope.UserId));

        //    if (user == null)
        //        return null;

        //    return string.Format("<@{0}>",envelope.UserId);
        //}
    }
}