using System;

namespace slackntell.Cli
{
    public static class Program
    {
        public static void Main()
        {
            var _token = "";
            using (var _teller = new Teller(
                host: "",
                port: 0, 
                username: "",
                password: "", 
                from: "",
                to: ""))
            using (var _slacker = new Slacker(_token, _teller))
            {
                _slacker.Start().Wait();

                Console.ReadLine();

                _slacker.Stop().Wait();
            }
        }
    }
}
