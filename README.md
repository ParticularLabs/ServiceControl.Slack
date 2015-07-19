# ServiceControl.Slack
Integrates with ServiceControl and provided notifications in Slack for failed messages, missing heartbeats etc

## Hosting

The integration comes with its own host but you can choose to host in in any endpoint if you want full control.

### Installing the host as a windows service

`PS C:\Users\Administrator> sc.exe create ServiceControl.Slack binpath= "C:\Program Files (x86)\ServiceControl.Slack\ServiceControl.Slack.Host.exe"`

