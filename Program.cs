using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Serilog;
using Serilog.Events;

var loggers = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .Enrich.FromLogContext();

loggers.WriteTo.Logger(logger => logger
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Verbose, outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

Log.Logger = loggers.CreateLogger();
Log.Logger.Information("Logger is initialized");
EventSourceListener.Logger = Log.Logger;
_ = new EventSourceListener();

var clients = Environment.GetCommandLineArgs().Skip(1).Select(arg => new Health.HealthClient(GrpcChannel.ForAddress(new Uri(arg)))).ToArray();

while (true)
{
    await Task.WhenAll(clients.Select(client => client.CheckAsync(new ()
    {
        Service = "live"
    }).ResponseAsync));
    var delay = Math.Pow(RandomNumberGenerator.GetInt32((int) 1E9) / 1E9, 2) * 1200000;
    Log.Debug("Sleeping {Sleep} ms ", delay);
    await Task.Delay(TimeSpan.FromMilliseconds(delay));
}

internal sealed class EventSourceListener : EventListener
{
    internal static ILogger? Logger { get; set; }

    private readonly Regex eventSourceNameRegex = new(@"^(System\.Net\.Http|System\.Net\.Sockets|System\.Net\.NameResolution|Grpc\.Net\.Client|System\.Net\.Security)$");

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        Logger!.Debug("New event source {EventSourceName}", eventSource.Name);
        if (eventSourceNameRegex.IsMatch(eventSource.Name))
        {
            EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        base.OnEventWritten(eventData);

        if (!eventSourceNameRegex.IsMatch(eventData.EventSource.Name))
        {
            return;
        }

        if (eventData.EventId == -1)
        {
            return;
        }

        if (eventData.Payload == null || eventData.Payload.Count == 0)
        {
            Logger!.Debug("Event {EventSource} {Event}", eventData.EventSource.Name, eventData.EventName);
        }
        else if (eventData.EventSource.Name == "System.Net.Http" && eventData.EventName == "RequestStart")
        {
            var scheme = eventData.Payload[eventData.PayloadNames!.IndexOf("scheme")];
            var host = eventData.Payload[eventData.PayloadNames!.IndexOf("host")];
            var pathAndQuery = eventData.Payload[eventData.PayloadNames!.IndexOf("pathAndQuery")];
            Logger!.Debug("Event {EventSource} {Event} {Scheme} {Host} {PathAndQuery}", eventData.EventSource.Name, eventData.EventName, scheme, host, pathAndQuery);
        }
        else
        {
            for (var i = 0; i < eventData.Payload.Count; ++i)
            {
                string payload;
                if (eventData.Payload[i] is string s)
                {
                    payload = s;
                }
                else
                {
                    payload = JsonSerializer.Serialize(eventData.Payload);
                }

                Logger!.Debug("Event {EventSource} {Event} {PayloadName} {Payload}", eventData.EventSource.Name, eventData.EventName, eventData.PayloadNames![i], payload);
            }
        }
    }
}
