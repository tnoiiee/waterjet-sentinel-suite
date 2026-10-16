using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The resolver defect class: missing configuration, present-but-rejected values, normalization
/// and containment. Every value here is SYNTHETIC: no real directory, hash or expected version
/// from the Owner-local evidence appears in this project, and none may.
/// </summary>
public sealed class ProbeConfigurationResolverTests
{
    private const string ValidDirectory = "C:\\Synthetic\\Approved\\Library";
    private const string ValidHashA = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private const string ValidHashB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string ValidVersion = "9.9.9-test";

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            map[name] = value;
        }

        return name => map.TryGetValue(name, out var value) ? value : null;
    }

    private static Func<string, string?> ValidEnv() => Env(
        (MotionLibraryProbeVocabulary.NativeDirectoryVariable, ValidDirectory),
        (MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable, ValidHashA),
        (MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable, ValidHashB),
        (MotionLibraryProbeVocabulary.ExpectedVersionVariable, ValidVersion));

    [Fact]
    public void A_Complete_Owner_Local_Value_Set_Resolves_To_A_Normalized_Redacted_Configuration()
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(
            ValidEnv(), out var configuration, out var refusalCode, out var refusalDetail);

        Assert.True(resolved, $"expected resolution, refused with {refusalCode}: {refusalDetail}");
        Assert.Equal(string.Empty, refusalCode);
        Assert.Equal(ValidDirectory, configuration.NativeDirectory);
        Assert.Equal(Path.Combine(ValidDirectory, "gclib.dll"), configuration.PrimaryModulePath);
        Assert.Equal(Path.Combine(ValidDirectory, "gclibo.dll"), configuration.CompanionModulePath);
        Assert.Equal(32, configuration.ExpectedPrimaryHash.Length);
        Assert.Equal(32, configuration.ExpectedCompanionHash.Length);
        Assert.Equal(ValidVersion, configuration.ExpectedVersion);
        Assert.Equal((byte)0x01, configuration.ExpectedPrimaryHash[0]);
        Assert.Equal((byte)0x23, configuration.ExpectedPrimaryHash[1]);

        // The fallback: the recorded companion hash parses with lowercase letters intact.
        Assert.Equal((byte)0xAB, configuration.ExpectedCompanionHash[0]);
    }

    [Fact]
    public void Every_Missing_Or_Blank_Required_Value_Is_A_Configuration_Missing_Refusal()
    {
        var variables = new[]
        {
            MotionLibraryProbeVocabulary.NativeDirectoryVariable,
            MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable,
            MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable,
            MotionLibraryProbeVocabulary.ExpectedVersionVariable,
        };

        foreach (var missing in variables)
        {
            var resolved = MotionLibraryConfigurationResolver.TryResolve(
                name => name == missing ? null : ValidEnv()(name),
                out _, out var refusalCode, out var refusalDetail);

            Assert.False(resolved, $"{missing} absent must refuse");
            Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirNotConfigured, refusalCode);
            Assert.Contains(missing, refusalDetail, StringComparison.Ordinal);

            resolved = MotionLibraryConfigurationResolver.TryResolve(
                name => name == missing ? "   " : ValidEnv()(name),
                out _, out refusalCode, out refusalDetail);

            Assert.False(resolved, $"{missing} blank must refuse");
            Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirNotConfigured, refusalCode);
        }
    }

    [Theory]
    [InlineData("Relative\\Path\\Library")]                    // not fully qualified
    [InlineData("\\Rooted\\Without\\Drive")]                   // rooted but driveless
    [InlineData("\\\\fileserver\\share\\library")]             // UNC
    [InlineData("C:\\Synthetic\\Approved\\*")]                 // wildcard star
    [InlineData("C:\\Synthetic\\Approved?\\Library")]          // wildcard question
    [InlineData("C:\\Synthetic%WINDIR%\\Library")]             // expansion token
    [InlineData("C:\\Synthetic:Stream\\Library")]              // stray colon
    [InlineData("C:\\Synthetic\"Quote\\Library")]              // quote character
    public void A_Present_But_Malformed_Directory_Is_A_Rejected_Refusal(string directory)
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(
            name => name == MotionLibraryProbeVocabulary.NativeDirectoryVariable ? directory : ValidEnv()(name),
            out _, out var refusalCode, out var refusalDetail);

        Assert.False(resolved, $"directory '{directory}' must refuse");
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirRejected, refusalCode);
        // The rejected VALUE is never echoed back: shape rules are stated, the value is not.
        Assert.DoesNotContain(directory.Trim('\"'), refusalDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0123")]                                                                   // too short
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDE")]        // 63 chars
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0")]      // 65 chars
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCG")]         // 63 hex + G
    [InlineData("01-234-56789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABC")]        // separators
    [InlineData("0x0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABC")]        // prefix
    public void A_Present_But_Malformed_Expected_Hash_Is_A_Rejected_Refusal(string hash)
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(
            name => name == MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable ? hash : ValidEnv()(name),
            out _, out var refusalCode, out var refusalDetail);

        Assert.False(resolved, $"hash of shape length {hash.Length} must refuse");
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirRejected, refusalCode);
        Assert.DoesNotContain(hash.Trim('x', '-'), refusalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_Separators_Are_Normalized_Without_Changing_The_Approved_Directory()
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(
            name => name == MotionLibraryProbeVocabulary.NativeDirectoryVariable ? ValidDirectory + "\\" : ValidEnv()(name),
            out var configuration, out _, out _);

        Assert.True(resolved);
        Assert.Equal(ValidDirectory, configuration.NativeDirectory);
    }

    [Fact]
    public void The_Resolved_Configuration_Never_Renders_Its_Values()
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(ValidEnv(), out var configuration, out _, out _);
        Assert.True(resolved);

        var rendered = configuration.ToString();
        Assert.Equal(MotionLibraryProbeVocabulary.ConfigurationRedacted, rendered);
        Assert.DoesNotContain(ValidDirectory, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ValidHashA, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ValidHashB, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ValidVersion, rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeConfiguration_Reports_Only_The_Redacted_Identity()
    {
        var resolved = MotionLibraryConfigurationResolver.TryResolve(ValidEnv(), out var configuration, out _, out _);
        Assert.True(resolved);

        Assert.Equal(
            MotionLibraryProbeVocabulary.ConfigurationRedacted,
            MotionLibraryConfigurationResolver.DescribeConfiguration(configuration));
        Assert.Equal(
            "MOTION_LIBRARY_CONFIGURATION_NOT_CONFIGURED",
            MotionLibraryConfigurationResolver.DescribeConfiguration(null));
    }
}
