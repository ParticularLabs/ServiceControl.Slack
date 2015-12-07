namespace ServiceControl.Slack
{
    using global::Slack.Webhooks;

    public class SlackNotifier
    {
        readonly SlackClient _client;
        readonly string _channel;

        public SlackNotifier(string webhookUrl, string channel = null)
        {
            _client = new SlackClient(webhookUrl);
            _channel = channel;
        }

        public void Notify(string message)
        {
            _client.Post(new SlackMessage
            {
                Channel = _channel,
                Text = message
            });
        }
    }
}