#slackntell

**Sends email notifications on new Slack messages (with some throttling)**

A simple CLI utility watching for new messages via [Slack Real Time Messaging API](https://api.slack.com/rtm). Built in C# .NET using [Inumedia/SlackAPI](https://github.com/Inumedia/SlackAPI) and [jstedfast/MailKit](https://github.com/jstedfast/MailKit).

To use it `source/slackntell.Cli/Program.cs` must be updated with a Slack token, SMTP credentials and to/from addresses.
