namespace ReadyToGoTravel.Support.Scanning;

public sealed class ClamAvOptions
{
    public const string SectionName = "Support:Scanning:ClamAv";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 3310;

    public int ConnectTimeoutMilliseconds { get; set; } = 5000;

    public int ReadTimeoutMilliseconds { get; set; } = 15000;
}
