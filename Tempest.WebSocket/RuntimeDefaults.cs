namespace Tempest.WebSocket;

public static class RuntimeDefaults
{
    public static readonly TimeSpan[] ReconnectDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    };

    public static readonly TimeSpan HealthStaleThreshold = TimeSpan.FromSeconds(15);

    public const int DefaultMaxMessageBytes = 256 * 1024;
}
