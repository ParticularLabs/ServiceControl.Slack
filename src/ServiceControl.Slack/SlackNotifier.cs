namespace ServiceControl.Slack
{
    using ServiceControl.Slack.Api;

    public class SlackNotifier
    {
        readonly SlackAdapter adapter;
        readonly string room;

        public SlackNotifier(SlackAdapter adapter, string room)
        {
            this.adapter = adapter;
            this.room = room;
        }

        public void Notify(string message)
        {
            adapter.Send(room, message).GetAwaiter().GetResult();
        }
    }
}