namespace ServiceControl.Slack.Api
{
    public class Message
    {
        public Message(Envelope envelope, string text)
        {
            Envelope = envelope;
            Text = text;
        }

        public Envelope Envelope { get; set; }

        public string Text { get; set; }
    }
}