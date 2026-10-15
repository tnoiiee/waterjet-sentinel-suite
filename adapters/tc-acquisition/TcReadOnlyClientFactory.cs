namespace Wjss.Adapters.TcAcquisition;

/// <summary>Application-owned construction boundary. Callers must enforce activation before calling.</summary>
public static class TcReadOnlyClientFactory
{
    public static ITcReadOnlyClient Create() => new FluentModbusTcClient();

    /// <summary>Explicit non-Hardware Product seam; never creates the physical transport.</summary>
    public static ITcReadOnlyClient CreateDeterministic() => new DeterministicTcReadOnlyClient();
}
