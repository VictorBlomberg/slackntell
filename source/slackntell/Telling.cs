using System;

namespace slackntell
{
    public class Telling
    {
        public Telling(DateTime timestamp, string user, string message)
        {
            Timestamp = timestamp;
            User = user;
            Message = message;
        }

        public DateTime Timestamp { get; }

        public string User { get; }

        public string Message { get; }
    }
}
