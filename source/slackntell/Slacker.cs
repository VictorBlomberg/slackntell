using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlackAPI;
using SlackAPI.WebSocketMessages;

namespace slackntell
{
    public class Slacker : IDisposable
    {
        private readonly Teller _Teller;
        private readonly string _SelfUserId;
        private readonly SlackSocketClient _SocketClient;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _LastSends;
        private Dictionary<string, User> _Users;
        private Dictionary<string, Channel> _Channels;
        private Dictionary<string, Channel> _Groups;
        private Dictionary<string, DirectMessageConversation> _DmConversations;

        public Slacker(string token, Teller teller, string selfUserId = null)
        {
            _Teller = teller;
            _SelfUserId = selfUserId;
            _SocketClient = new SlackSocketClient(token);
            _LastSends = new ConcurrentDictionary<string, DateTimeOffset>();
        }

        public async Task Start()
        {
            await _WaitForResponse<LoginResponse>(_done => _SocketClient.Connect(
                _response =>
                    {
                        _done(_response);
                    },
                () =>
                    {
                    })).ConfigureAwait(false);

            var _usersResponseTask = _WaitForResponse<UserListResponse>(_done => _SocketClient.GetUserList(_done));
            var _channelsResponseTask = _WaitForResponse<ChannelListResponse>(_done => _SocketClient.GetChannelList(_done));
            var _dmConversationsResponseTask = _WaitForResponse<DirectMessageConversationListResponse>(_done => _SocketClient.GetDirectMessageList(_done));
            var _groupsResponseTask = _WaitForResponse<GroupListResponse>(_done => _SocketClient.GetGroupsList(_done));

            _Users = (await _usersResponseTask.ConfigureAwait(false)).members.ToDictionary(_record => _record.id);
            _Channels = (await _channelsResponseTask.ConfigureAwait(false)).channels.ToDictionary(_record => _record.id);
            _Groups = (await _groupsResponseTask.ConfigureAwait(false)).groups.ToDictionary(_record => _record.id);
            _DmConversations = (await _dmConversationsResponseTask.ConfigureAwait(false)).ims.ToDictionary(_record => _record.id);
            
            _SocketClient.OnMessageReceived += _SocketClientOnOnMessageReceived;
        }

        public Task Stop()
        {
            return Task.Run(() =>
                {
                    _SocketClient.OnMessageReceived -= _SocketClientOnOnMessageReceived;
                    _SocketClient.CloseSocket();
                });
        }

        public void Dispose()
        {
            _SocketClient.OnMessageReceived -= _SocketClientOnOnMessageReceived;
            _SocketClient.CloseSocket();
        }

        private void _SocketClientOnOnMessageReceived(NewMessage newMessage)
        {
            Task.Run(async () =>
                {
                    if (_SelfUserId != null && string.Equals(newMessage.user, _SelfUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var _now = DateTimeOffset.UtcNow;
                    var _limit = _now.Subtract(TimeSpan.FromMinutes(10));
                    var _lastSend = _LastSends.AddOrUpdate(newMessage.channel, _now, (_channel, _last) => _last > _limit ? _last : _now);

                    if (_lastSend != _now)
                    {
                        await Task.Delay(_lastSend - _limit).ConfigureAwait(false);

                        if (!_LastSends.TryUpdate(newMessage.channel, _now, _lastSend))
                        {
                            return;
                        }
                    }

                    var _user = _GetDisplayName(newMessage.user);
                    var _context = _GetDisplayName(newMessage.channel);

                    Message[] _messages;
                    try
                    {
                        if (_Channels.ContainsKey(newMessage.channel))
                        {
                            _messages = (await _WaitForResponse<ChannelMessageHistory>(_done => _SocketClient.GetChannelHistory(_done, _Channels[newMessage.channel])).ConfigureAwait(false))?.messages;
                        }
                        else if (_Groups.ContainsKey(newMessage.channel))
                        {
                            _messages = (await _WaitForResponse<GroupMessageHistory>(_done => _SocketClient.GetGroupHistory(_done, _Groups[newMessage.channel])).ConfigureAwait(false))?.messages;
                        }
                        else if (_DmConversations.ContainsKey(newMessage.channel))
                        {
                            _messages = (await _WaitForResponse<MessageHistory>(_done => _SocketClient.GetDirectMessageHistory(_done, _DmConversations[newMessage.channel])).ConfigureAwait(false))?.messages;
                        }
                        else
                        {
                            _messages = null;
                        }
                    }
                    catch
                    {
                        _messages = null;
                    }

                    var _history = _messages?
                        .Where(_message => _message.ts.ToUniversalTime() > DateTime.UtcNow.Subtract(TimeSpan.FromHours(24)))
                        .Select(_message => new Telling(_message.ts, _GetDisplayName(_message.user), _message.text))
                        ?? Enumerable.Empty<Telling>();
                    _Teller.Rat(_context, _user, _history);
                });
        }
        
        private string _GetDisplayName(string id)
        {
            User _user;
            if (_Users.TryGetValue(id, out _user))
            {
                return _user.name;
            }

            Channel _channel;
            if (_Channels.TryGetValue(id, out _channel) || _Groups.TryGetValue(id, out _channel))
            {
                return _channel.name;
            }

            DirectMessageConversation _dmConversation;
            if (_DmConversations.TryGetValue(id, out _dmConversation))
            {
                return _GetDisplayName(_dmConversation.user);
            }

            return null;
        }
        
        private async Task<T> _WaitForResponse<T>(Action<Action<T>> action)
            where T : Response
        {
            var _completionSource = new TaskCompletionSource<T>();
            action(_result =>
                {
                    if (_result.ok)
                        _completionSource.SetResult(_result);
                    else
                        _completionSource.SetException(new SlackerResponseException(_result));
                });
            return await _completionSource.Task.ConfigureAwait(false);
        }

        public sealed class SlackerResponseException : Exception
        {
            public SlackerResponseException(Response response)
                : base(response?.error)
            {
            }
        }
    }
}
