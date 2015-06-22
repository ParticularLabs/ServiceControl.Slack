namespace ServiceControl.Slack
{
    using NServiceBus;
    using ServiceControl.Contracts;

    public class HeartbeatStoppedHandler : IHandleMessages<HeartbeatStopped>
    {
        readonly SlackNotifier notifier;

        public HeartbeatStoppedHandler(SlackNotifier notifier)
        {
            this.notifier = notifier;
        }

        public void Handle(HeartbeatStopped message)
        {
            notifier.Notify(string.Format("Endpoint {0} seems to have stopped sending heartbeats", message.EndpointName));
        }
    }
}