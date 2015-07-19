namespace ServiceControl.Slack
{
    using NServiceBus;
    using ServiceControl.Contracts;

    public class HeartbeatRestoredHandler : IHandleMessages<HeartbeatRestored>
    {
        readonly SlackNotifier notifier;

        public HeartbeatRestoredHandler(SlackNotifier notifier)
        {
            this.notifier = notifier;
        }

        public void Handle(HeartbeatRestored message)
        {
            notifier.Notify(string.Format("Endpoint `{0}` has now resumed sending heartbeats", message.EndpointName));
        }
    }
}