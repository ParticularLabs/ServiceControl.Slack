namespace ServiceControl.Slack.Api
{
    public enum EnvelopeType
    {
        Console,
        Channel,
        Im
    }

    public class Envelope
    {
        public Envelope(string userId, string dmId, string channelId, EnvelopeType type)
        {
            UserId = userId;
            DmId = dmId;
            ChannelId = channelId;
            EnvelopeType = type;
        }

        public string UserId { get; private set; }

        public string DmId { get; private set; }

        public string ChannelId { get; private set; }

        public EnvelopeType EnvelopeType { get; private set; }
    }
}