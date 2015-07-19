using System;
using System.ServiceProcess;
using NServiceBus;
using NServiceBus.Logging;
using ServiceControl.Slack;
using ServiceControl.Slack.Api;

class ProgramService : ServiceBase
{
    static void Main()
    {
        using (var service = new ProgramService())
        {
            // so we can run interactive from Visual Studio or as a windows service
            if (Environment.UserInteractive)
            {
                service.OnStart(null);
                Console.WriteLine("\r\nPress enter key to stop program\r\n");
                Console.Read();
                service.OnStop();
                return;
            }
            Run(service);
        }
    }

    protected override void OnStart(string[] args)
    {
        try
        {
            var busConfiguration = new BusConfiguration();
            busConfiguration.EndpointName("ServiceControl.Slack");
            busConfiguration.UseSerialization<JsonSerializer>();
            busConfiguration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts"));
            busConfiguration.DefineCriticalErrorAction(OnCriticalError);

            busConfiguration.UsePersistence<InMemoryPersistence>();

            busConfiguration.EnableInstallers();

            var token = Environment.GetEnvironmentVariable("ServiceControl.Slack.Token", EnvironmentVariableTarget.User);

            if (token == null)
            {
                throw new Exception("Couldn't find a slack api token, please add a user env variable named `ServiceControl.Slack.Token`");
            }

            slackAdapter = new SlackAdapter(token);

            var roomName = Environment.GetEnvironmentVariable("ServiceControl.Slack.RoomName", EnvironmentVariableTarget.User) ?? "servicecontrol";

            busConfiguration.RegisterComponents(c => c.RegisterSingleton(new SlackNotifier(slackAdapter, roomName)));

            var startableBus = Bus.Create(busConfiguration);

            slackAdapter.Start().GetAwaiter().GetResult();

            bus = startableBus.Start();

            logger.InfoFormat("Integration is now active, notifications will be feed into room '{0}'", roomName);
        }
        catch (Exception exception)
        {
            OnCriticalError("Failed to start the bus.", exception);
        }
    }


    void OnCriticalError(string errorMessage, Exception exception)
    {
        var fatalMessage = string.Format("The following critical error was encountered:\n{0}\nProcess is shutting down.", errorMessage);
        logger.Fatal(fatalMessage, exception);
        Environment.FailFast(fatalMessage, exception);
    }

    protected override void OnStop()
    {
        if (slackAdapter != null)
        {
            slackAdapter.Stop().GetAwaiter().GetResult();
        }

        if (bus != null)
        {
            bus.Dispose();
        }
    }

    IBus bus;
    SlackAdapter slackAdapter;

    static ILog logger = LogManager.GetLogger<ProgramService>();

}