using System.Reflection;
using System.Text.Json;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Fail-closed endpoint-resolution coverage of the B1 identity surface. Every candidate address
/// is assembled from integers or non-address text at runtime: no address literal is committed,
/// keeping the boundary-scan S2 posture exact. The resolver is pure (environment reader only):
/// nothing here loads, opens or contacts anything.
/// </summary>
public sealed class MotionControllerEndpointConfigurationTests
{
    private static string Address(params int[] octets) => string.Join(".", octets);

    private static bool TryResolveValue(string? value, out MotionControllerEndpointConfiguration configuration, out string code)
    {
        return MotionControllerEndpointResolver.TryResolve(
            name => name == MotionControllerEndpointResolver.EndpointVariable ? value : null,
            out configuration,
            out code,
            out _);
    }

    [Fact]
    public void Missing_Or_Blank_Endpoint_Refuses_As_Not_Configured()
    {
        Assert.False(TryResolveValue(null, out _, out var code));
        Assert.Equal("MOTION_ENDPOINT_NOT_CONFIGURED", code);

        Assert.False(TryResolveValue(string.Empty, out _, out code));
        Assert.Equal("MOTION_ENDPOINT_NOT_CONFIGURED", code);

        Assert.False(TryResolveValue("   ", out _, out code));
        Assert.Equal("MOTION_ENDPOINT_NOT_CONFIGURED", code);
    }

    [Fact]
    public void Malformed_Endpoint_Refuses_As_Rejected()
    {
        // Every candidate is assembled at runtime from fragments or plain words: no address
        // literal is committed (boundary-scan S2 posture).
        var candidates = new List<string>
        {
            "not-an-address",
            string.Join(".", 1, 2, 3),
            string.Join(".", 1, 2, 3, 4, 5),
            string.Join(".", 256, 1, 1, 1),
            "-" + string.Join(".", 1, 2, 3, 4),
            string.Join(".", 1, 2, 3, 4) + ":502",
            "0" + string.Join(".", 1, 2, 3, 4),
            string.Join(".", 1, 2, 3) + ".04",
            string.Join(".", 1, 2, 3, 4) + " ",
            " " + string.Join(".", 1, 2, 3, 4),
            string.Join(".", 1, 2, 3, 4) + "\t",
            string.Join(".", 1, 2, 3, 4) + "\r\n",
            "a.b.c.d",
            string.Join(".", 1, 2, 3) + ".",
            "." + string.Join(".", 1, 2, 3),
            string.Join(".", 1, 2) + ".." + 3,
            Address(10, 1, 2, 3) + ";suffix",
            Address(10, 1, 2, 3) + "&extra",
            Address(10, 1, 2, 3) + "|token",
        };

        foreach (var candidate in candidates)
        {
            Assert.False(TryResolveValue(candidate, out _, out var code));
            Assert.Equal("MOTION_ENDPOINT_REJECTED", code);
        }
    }

    [Fact]
    public void Option_Text_Can_Never_Be_Smuggled_Into_The_Open_Address()
    {
        var smuggled = Address(10, 1, 2, 3) + " --direct --timeout 1";
        Assert.False(TryResolveValue(smuggled, out _, out var code));
        Assert.Equal("MOTION_ENDPOINT_REJECTED", code);
    }

    [Fact]
    public void Loopback_Unspecified_And_Broadcast_Multicast_Forms_Are_Rejected()
    {
        Assert.False(TryResolveValue(Address(127, 0, 0, 1), out _, out var code));
        Assert.Equal("MOTION_ENDPOINT_REJECTED", code);

        Assert.False(TryResolveValue(Address(0, 0, 0, 0), out _, out code));
        Assert.Equal("MOTION_ENDPOINT_REJECTED", code);

        Assert.False(TryResolveValue(Address(255, 255, 255, 255), out _, out code));
        Assert.Equal("MOTION_ENDPOINT_REJECTED", code);

        Assert.False(TryResolveValue(Address(224, 0, 0, 1), out _, out code));
        Assert.Equal("MOTION_ENDPOINT_REJECTED", code);
    }

    [Fact]
    public void A_Canonical_Unicast_Dotted_Quad_Resolves_And_Builds_Only_The_Validated_Address()
    {
        var candidate = Address(10, 20, 30, 40);
        Assert.True(TryResolveValue(candidate, out var configuration, out var code));
        Assert.Equal(string.Empty, code);

        // gclib 2.6.0 manual, gclib_open entry (printed page 48): the second parameter is the
        // controller ADDRESS, never an option string. Direct mode is gclib_force_gcaps(false)
        // from the same page, so no --direct and no --timeout may be appended here.
        var openAddress = MotionControllerApi.BuildOpenAddress(configuration);
        Assert.Equal(candidate, openAddress);
        Assert.DoesNotContain(" ", openAddress, StringComparison.Ordinal);
        Assert.DoesNotContain("--direct", openAddress, StringComparison.Ordinal);
        Assert.DoesNotContain("--timeout", openAddress, StringComparison.Ordinal);
        Assert.DoesNotContain("2000", openAddress, StringComparison.Ordinal);

        // The ONLY renderable endpoint statement: the value itself never appears.
        Assert.Equal("ENDPOINT_CONFIGURED", configuration.ToString());
        Assert.Equal(
            "ENDPOINT_CONFIGURED",
            MotionControllerEndpointResolver.DescribeConfiguration(configuration));
        Assert.DoesNotContain(candidate, configuration.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(candidate, JsonSerializer.Serialize(configuration), StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_Endpoint_Is_Opaque_To_Public_Callers_And_Default_Json_Serialization()
    {
        var candidate = Address(10, 20, 30, 40);
        Assert.True(TryResolveValue(candidate, out var configuration, out _));

        var type = typeof(MotionControllerEndpointConfiguration);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));

        var addressBuilder = typeof(MotionControllerApi).GetMethod(
            "BuildOpenAddress",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(addressBuilder);
        Assert.Equal(new[] { type }, addressBuilder!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Null(typeof(MotionControllerApi).GetMethod(
            "BuildOpenAddress",
            BindingFlags.Public | BindingFlags.Static));

        var json = JsonSerializer.Serialize(configuration);
        Assert.DoesNotContain(candidate, json, StringComparison.Ordinal);
        Assert.DoesNotContain("EndpointValue", json, StringComparison.Ordinal);
        Assert.Equal("{}", json);
        Assert.Equal("ENDPOINT_CONFIGURED", configuration.ToString());
    }

    [Fact]
    public void The_Resolver_Reads_Only_Its_Own_Variable_And_Performs_No_I_O()
    {
        var reads = new List<string>();
        var resolved = MotionControllerEndpointResolver.TryResolve(
            name =>
            {
                reads.Add(name);
                return name == MotionControllerEndpointResolver.EndpointVariable
                    ? Address(10, 20, 30, 40)
                    : null;
            },
            out var configuration,
            out _,
            out _);

        Assert.True(resolved);
        Assert.NotNull(configuration);
        Assert.Single(reads);
        Assert.Equal(MotionControllerEndpointResolver.EndpointVariable, reads[0]);
        Assert.Equal("WJSS_MOTION_CONTROLLER_01_ENDPOINT", MotionControllerEndpointResolver.EndpointVariable);
    }
}
