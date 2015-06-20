namespace ServiceControl.Slack
{
    using NServiceBus;
    using ServiceControl.Contracts;

    public class FailedEndpointHandler:IHandleMessages<HeartbeatStopped>
    {
        public void Handle(HeartbeatStopped message)
        {
            
        }
    }
}