namespace ServiceControl.Slack.Tests
{
    using System;
    using NUnit.Framework;

    public class SlackAdapterIntegrationTests
    {
        [Test]
        [Explicit]
        public void PostMessage()
        {
            var token = Environment.GetEnvironmentVariable("ServiceControl.Slack.Token", EnvironmentVariableTarget.User);

            if (token == null)
            {
                throw new Exception("Couldn't find an slack api token, please add a user env variable named `ServiceControl.Slack.Token`");
            }

            var api = new SlackNotifier(token);

            api.Notify("Hello from integration tests");

            Console.Out.WriteLine("All seems good, please check: https://particular-test.slack.com/messages/sc-bot-test/");
        }
    }
}