using Wjss.Contracts;
using Xunit;

namespace Wjss.Domain.Tests;

/// <summary>
/// The profile-startup boundary is a safety-relevant rule; the tests below are
/// the executable record of it. Only SIMULATOR starts in Stage 0.3A.
/// </summary>
public sealed class ProfileStartPolicyTests
{
    [Theory]
    [InlineData(DeviceProfile.SIMULATOR, true)]
    [InlineData(DeviceProfile.TEST_HARDWARE, false)]
    [InlineData(DeviceProfile.PRODUCTION, false)]
    public void Only_Simulator_Starts(DeviceProfile profile, bool expected)
    {
        Assert.Equal(expected, ProfileStartPolicy.IsStartable(profile));

        var ok = ProfileStartPolicy.TryRequireStartable(profile, out var code, out var reason);
        Assert.Equal(expected, ok);
        if (!expected)
        {
            Assert.Equal(ProfileStartPolicy.RefusalCode, code);
            Assert.Contains("Scope Gate", reason, StringComparison.Ordinal);
            Assert.Contains("SIMULATOR", reason, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(string.Empty, code);
        }
    }

    [Theory]
    [InlineData("SIMULATOR", DeviceProfile.SIMULATOR, true)]
    [InlineData("simulator", DeviceProfile.SIMULATOR, true)]
    [InlineData("PRODUCTION", DeviceProfile.PRODUCTION, true)] // parses; startup still refused by policy
    [InlineData("", DeviceProfile.SIMULATOR, false)]
    [InlineData("NOT-A-PROFILE", (DeviceProfile)0, false)]
    public void Parsing_Is_Strict_And_Never_Implicit(string? label, DeviceProfile expected, bool expectParsed)
    {
        var parsed = ProfileStartPolicy.TryParseProfile(label, out var profile);
        Assert.Equal(expectParsed, parsed);
        if (expectParsed)
        {
            Assert.Equal(expected, profile);
        }
    }
}
