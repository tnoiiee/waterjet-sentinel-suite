namespace Wjss.Adapters.TcAcquisition;

/// <summary>Application-owned construction boundary. Callers must enforce activation before calling.</summary>
public static class TcReadOnlyClientFactory
{
    public static ITcReadOnlyClient Create() => new FluentModbusTcClient();
}
