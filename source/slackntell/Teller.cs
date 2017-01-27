using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace slackntell
{
    public sealed class Teller : IDisposable
    {
        private readonly string _Host;
        private readonly int _Port;
        private readonly string _Username;
        private readonly string _Password;
        private readonly string _From;
        private readonly string _To;

        public Teller(string host, int port, string username, string password, string from, string to)
        {
            _Host = host;
            _Port = port;
            _Username = username;
            _Password = password;
            _From = from;
            _To = to;
        }

        public void Rat(string context, string user, IEnumerable<Telling> tellings)
        {
            var _bodyBuilder = new BodyBuilder();
            _bodyBuilder.TextBody = string.Join("\n", tellings.Select(_telling => $"{_telling.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} @{_telling.User}: {_telling.Message}"));

            var _mimeMessage = new MimeMessage();
            _mimeMessage.From.Add(new MailboxAddress(_From));
            _mimeMessage.To.Add(new MailboxAddress(_To));
            _mimeMessage.Subject = string.Join(
                " ",
                "[slackntell]",
                context == null ? null : $"#{context}",
                user == null ? null : $"@{user}");
            _mimeMessage.Body = _bodyBuilder.ToMessageBody();

            using (var _client = new SmtpClient())
            {
                _client.Connect(_Host, _Port, SecureSocketOptions.SslOnConnect);
                _client.Authenticate(_Username, _Password);
                _client.Send(_mimeMessage);
                _client.Disconnect(true);
            }
        }
        
        public void Dispose()
        {
        }
    }
}
