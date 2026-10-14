namespace Wjss.TcBench;

/// <summary>
/// Local TCP Simulation: the Owner-local loopback execution class of the bench.
///
/// It is the third, explicitly separate execution class next to the no-I/O dry run (SIMULATION)
/// and the authorized physical trial (PHYSICAL_DIAGNOSTIC). It exists so the Owner can exercise
/// the real application-owned acquisition path against a simulator listening on the local host
/// without editing Source, without the trusted physical candidate changing, and without any
/// physical authorization, capability flag or gate being involved.
///
/// Separation of concerns:
/// - the trusted catalog tuple stays the DEFAULT configuration (template): it provides the request
///   values and the physical candidate address, and it grants no execution authority;
/// - <see cref="InvocationConfiguration"/> is the INVOCATION configuration of one local run: the
///   same tuple with only the address replaced by the literal IPv4 loopback text;
/// - physical authorization is untouched: nothing here produces a <see cref="PhysicalTrialPlan"/>,
///   and a loopback address can never satisfy the exact physical tuple binding.
///
/// Fail-closed boundary, enforced before any client is constructed and never corrected:
/// address text must equal <see cref="LoopbackAddress"/> exactly (no other IPv4, no IPv6, no host
/// name, no resolution, no alternative spelling), the port must be a valid TCP port, and the
/// request must be exactly the trusted default request (FC03 holding registers, Unit 0, start 0,
/// quantity 40, BigEndian). The command line always invokes <see cref="LoopbackAddress"/>:<see cref="SimulatorPort"/>.
/// A local result is never T01, never hardware evidence and never accepted: it stays QUARANTINED.
/// </summary>
public static class LocalSimulation
{
    /// <summary>Canonical execution class recorded as evidence provenance. Never a physical class.</summary>
    public const string Provenance = "LOCAL_TCP_SIMULATION";

    /// <summary>The only accepted Local Simulation address: the literal IPv4 loopback text.</summary>
    public const string LoopbackAddress = "127.0.0.1";

    /// <summary>The port the Owner-local simulator listens on; the command-line invocation always uses it.</summary>
    public const int SimulatorPort = 502;

    /// <summary>
    /// The invocation configuration of one Owner-local simulator run: the trusted default tuple with
    /// only the address replaced by the literal loopback text. The default address itself is read
    /// from the catalog and is never typed here.
    /// </summary>
    public static TrialConfiguration InvocationConfiguration(TrialCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return RequireValid(catalog, catalog.Select("T01").Trial with { Address = LoopbackAddress, Port = SimulatorPort });
    }

    /// <summary>
    /// Fail-closed validation of one invocation configuration. Throws before any client exists;
    /// nothing is substituted, corrected or resolved. Returns the validated configuration unchanged.
    /// </summary>
    public static TrialConfiguration RequireValid(TrialCatalog catalog, TrialConfiguration invocation)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(invocation);
        var trusted = catalog.Select("T01").Trial;
        // Literal loopback text only: the configured physical address, any other IPv4, IPv6, a host
        // name or any alternative spelling is refused here, so no resolution can ever widen the boundary.
        if (!string.Equals(invocation.Address, LoopbackAddress, StringComparison.Ordinal)) throw new ArgumentException("LOCAL_SIMULATION_ADDRESS_REJECTED");
        if (invocation.Port is < 1 or > 65535) throw new ArgumentException("LOCAL_SIMULATION_PORT_REJECTED");
        // Exactly the trusted default request and identity: FC03, Unit 0, start 0, quantity 40,
        // BigEndian, T01 / TC-NODE-01 / CAB1. Any other value is refused, never corrected.
        if (invocation with { Address = trusted.Address, Port = trusted.Port } != trusted) throw new ArgumentException("LOCAL_SIMULATION_REQUEST_REJECTED");
        invocation.Endpoint.Validate();
        return invocation;
    }
}
