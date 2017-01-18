using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlackAPI;
using SlackAPI.WebSocketMessages;

namespace slackntell
{
    public class Slacker : IDisposable
    {
        private readonly Teller _Teller;
        private readonly SlackSocketClient _SocketClient;
        private Dictionary<string, User> _Users;
        private Dictionary<string, Channel> _Channels;

        public Slacker(string token, Teller teller)
        {
            _Teller = teller;
            _SocketClient = new SlackSocketClient(token);
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

            var _usersResponse = await _WaitForResponse<UserListResponse>(_done => _SocketClient.GetUserList(_done)).ConfigureAwait(false);
            var _channelsResponse = await _WaitForResponse<ChannelListResponse>(_done => _SocketClient.GetChannelList(_done)).ConfigureAwait(false);

            _Users = _usersResponse.members
                .ToDictionary(_user => _user.id);
            _Channels = _channelsResponse.channels
                .ToDictionary(_channel => _channel.id);
            
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
            var _raw = JObject.FromObject(newMessage).ToString(Formatting.Indented);
            var _user = _GetUser(newMessage.user);
            var _channel = _GetChannel(newMessage.channel);
            _Teller.Rat(_channel?.name, _user?.name, newMessage.text, _raw);
        }

        private User _GetUser(string id)
        {
            User _user;
            return _Users.TryGetValue(id, out _user) ? _user : null;
        }

        private Channel _GetChannel(string id)
        {
            Channel _channel;
            return _Channels.TryGetValue(id, out _channel) ? _channel : null;
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
