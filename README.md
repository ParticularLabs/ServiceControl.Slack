# ServiceControl.Slack
Integrates with ServiceControl and provided notifications in Slack for failed messages, missing heartbeats etc.

At the moment only notifications for missing heartbeats are implemented

## Hosting

The integration comes with its own host but you can choose to host in in any endpoint if you want full control.

### Installing the host as a windows service

`PS C:\Users\Administrator> sc.exe create ServiceControl.Slack binpath= "C:\Program Files (x86)\ServiceControl.Slack\ServiceControl.Slack.Host.exe"`

### Configuration

1. You need to create a bot integration in Slack and provide the WebHook URL via a user environment variable called `ServiceControl.Slack.Token`.

2. You can optionally specify the channel where the notifications will be posted using a user environment variable called `ServiceControl.Slack.RoomName`. If you do not specify, the channel you have picked in the WebHook configuration will be used. 

3. If installing on a separate machine from where ServiceControl is running you need to modify the endpoint mappings in `ServiceControl.Slack.Host.exe.config` accordingly

See https://api.slack.com/incoming-webhooks for more details on WebHooks.