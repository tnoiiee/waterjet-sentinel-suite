using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The invocation gate for the isolated C1 Stop trial: exact mode/axis shape, one-use local
/// consent, existing endpoint/native configuration, and no native work before all preconditions.
/// All values are synthetic; native and dispatch kernels are never called by these tests.
/// </summary>
public sealed class MotionStopTrialInvocationTests
{
    private sealed class FakeEnvironment
    {
        internal Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
        internal List<string> Reads { get; } = new();
        internal List<string> Removals { get; } = new();
        internal bool ThrowOnConsentRead { get; set; }
        internal bool ThrowOnConsentRemoval { get; set; }

        internal string? Read(string name)
        {
            Reads.Add(name);
            if (ThrowOnConsentRead
                && string.Equals(name, MotionStopTrialVocabulary.ConsentVariable, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("synthetic environment failure");
            }

            return Values.TryGetValue(name, out var value) ? value : null;
        }

        internal void Remove(string name)
        {
            Removals.Add(name);
            if (ThrowOnConsentRemoval
                && string.Equals(name, MotionStopTrialVocabulary.ConsentVariable, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("synthetic environment failure");
            }

            Values.Remove(name);
        }
    }

    private const string SyntheticNativeDirectory = @"C:\Synthetic\Approved\Library";
    private const string SyntheticHashA = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private const string SyntheticHashB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string SyntheticVersion = "9.9.9-test";
    private static readonly string SyntheticAddress = string.Join(".", 10, 20, 30, 40);

    private static FakeEnvironment ValidEnvironment(bool includeConsent = true)
    {
        var environment = new FakeEnvironment();
        environment.Values[MotionLibraryProbeVocabulary.NativeDirectoryVariable] = SyntheticNativeDirectory;
        environment.Values[MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable] = SyntheticHashA;
        environment.Values[MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable] = SyntheticHashB;
        environment.Values[MotionLibraryProbeVocabulary.ExpectedVersionVariable] = SyntheticVersion;
        environment.Values[MotionControllerEndpointResolver.EndpointVariable] = SyntheticAddress;
        if (includeConsent)
        {
            environment.Values[MotionStopTrialVocabulary.ConsentVariable] =
                MotionStopTrialVocabulary.ExactConsentValue;
        }

        return environment;
    }

    private static MotionStopTrialInvocation CreateInvocation(
        FakeEnvironment environment,
        Func<MotionLibraryProbeConfiguration, MotionLibraryProbeOutcome>? gateAProbe = null,
        Action? onControllerKernelCreate = null,
        Action? onDispatchKernelCreate = null)
    {
        var probe = gateAProbe ?? (_ => MotionLibraryProbeOutcome.Refused(
            MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
            "synthetic refusal",
            nativeHandlesReleased: true));

        return new MotionStopTrialInvocation(
            environment.Read,
            environment.Remove,
            configuration => probe(configuration),
            () =>
            {
                onControllerKernelCreate?.Invoke();
                throw new InvalidOperationException("controller kernel must not be created in this test");
            },
            (_, _) =>
            {
                onDispatchKernelCreate?.Invoke();
                throw new InvalidOperationException("dispatch kernel must not be created in this test");
            });
    }

    private static string VisibleReport(MotionStopTrialOutcome outcome) => string.Join(
        "|",
        outcome.PreconditionCode,
        outcome.RefusalCode,
        outcome.ConsentCode,
        outcome.EndpointCode,
        outcome.NativeConfigurationCode,
        outcome.NativeProofCode,
        outcome.Axis,
        outcome.OpenAttempted,
        outcome.OpenClassification,
        outcome.StopCode,
        outcome.NativeDispatchAttempted,
        outcome.StopDispatched,
        outcome.StopConfirmed,
        outcome.CleanupCode,
        outcome.CleanupFailureCode,
        outcome.StopSessionInvalidated,
        outcome.ControllerHandleClosed,
        outcome.NativeModulesReleased,
        outcome.ExitCode);

    public static IEnumerable<object[]> InvalidInvocationShapes()
    {
        yield return new object[] { null! };
        yield return new object[] { Array.Empty<string>() };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "A", "B" } };
        yield return new object[] { new[] { "other-mode", "A" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "a" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "b" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "C" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "D" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "AB" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "A,B" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "ST A" } };
        yield return new object[] { new[] { MotionStopTrialVocabulary.ModeToken, "--help" } };
    }

    [Theory]
    [MemberData(nameof(InvalidInvocationShapes))]
    public void Only_The_Exact_Mode_And_One_Uppercase_A_Or_B_Are_Parsed(string[]? arguments)
    {
        Assert.False(MotionStopTrialInvocation.TryParseInvocation(arguments, out _));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void Exact_Axis_Tokens_Map_To_The_Closed_Stop_Enum(string token)
    {
        Assert.True(MotionStopTrialInvocation.TryParseInvocation(
            new[] { MotionStopTrialVocabulary.ModeToken, token },
            out var axis));
        Assert.Equal(token == "A" ? MotionStopAxis.A : MotionStopAxis.B, axis);
    }

    [Fact]
    public void Exact_Consent_Is_Removed_Before_Configuration_And_Gate_A()
    {
        var environment = ValidEnvironment();
        var gateACalls = 0;
        var controllerKernelCreates = 0;
        var dispatchKernelCreates = 0;
        var invocation = CreateInvocation(
            environment,
            _ =>
            {
                gateACalls++;
                return MotionLibraryProbeOutcome.Refused(
                    MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
                    "synthetic refusal",
                    nativeHandlesReleased: true);
            },
            () => controllerKernelCreates++,
            () => dispatchKernelCreates++);

        var outcome = invocation.Run(new[] { MotionStopTrialVocabulary.ModeToken, "A" });

        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentAccepted, outcome.ConsentCode);
        Assert.Equal(MotionStopTrialVocabulary.PreconditionAccepted, outcome.PreconditionCode);
        Assert.Equal(MotionControllerIdentityVocabulary.EndpointConfigured, outcome.EndpointCode);
        Assert.Equal(MotionLibraryProbeVocabulary.ConfigurationRedacted, outcome.NativeConfigurationCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform, outcome.NativeProofCode);
        Assert.Equal(1, gateACalls);
        Assert.Equal(0, controllerKernelCreates);
        Assert.Equal(0, dispatchKernelCreates);
        Assert.Equal(new[] { MotionStopTrialVocabulary.ConsentVariable }, environment.Removals);
        Assert.False(environment.Values.ContainsKey(MotionStopTrialVocabulary.ConsentVariable));
        Assert.Equal(MotionStopTrialVocabulary.ConsentVariable, environment.Reads[0]);
        Assert.False(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);

        var visible = VisibleReport(outcome);
        Assert.DoesNotContain(MotionStopTrialVocabulary.ExactConsentValue, visible, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticAddress, visible, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticHashA, visible, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticHashB, visible, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticVersion, visible, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticNativeDirectory, visible, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("incorrect")]
    [InlineData(" AUTHORIZE_ONE_NO_LOAD_STOP_TRIAL")]
    [InlineData("AUTHORIZE_ONE_NO_LOAD_STOP_TRIAL ")]
    [InlineData("authorize_one_no_load_stop_trial")]
    public void Consent_Must_Be_An_Exact_Match_And_Is_Consumed_On_Refusal(string supplied)
    {
        var environment = ValidEnvironment();
        environment.Values[MotionStopTrialVocabulary.ConsentVariable] = supplied;
        var gateACalls = 0;
        var invocation = CreateInvocation(environment, _ =>
        {
            gateACalls++;
            return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
        });

        var outcome = invocation.Run(new[] { MotionStopTrialVocabulary.ModeToken, "A" });

        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentRejected, outcome.ConsentCode);
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentRejected, outcome.RefusalCode);
        Assert.Equal(0, gateACalls);
        Assert.False(environment.Values.ContainsKey(MotionStopTrialVocabulary.ConsentVariable));
        Assert.DoesNotContain(supplied, VisibleReport(outcome), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_Consent_Is_Removed_And_Refused_Before_Configuration_Reads()
    {
        var environment = ValidEnvironment(includeConsent: false);
        var gateACalls = 0;

        var outcome = CreateInvocation(environment, _ =>
        {
            gateACalls++;
            return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
        }).Run(new[] { MotionStopTrialVocabulary.ModeToken, "B" });

        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentNotConfigured, outcome.ConsentCode);
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentNotConfigured, outcome.RefusalCode);
        Assert.Equal(0, gateACalls);
        Assert.Equal(new[] { MotionStopTrialVocabulary.ConsentVariable }, environment.Reads);
        Assert.Equal(new[] { MotionStopTrialVocabulary.ConsentVariable }, environment.Removals);
    }

    [Fact]
    public void Consent_Read_Or_Removal_Failure_Fails_Closed_Without_Configuration_Reads()
    {
        var readEnvironment = ValidEnvironment();
        readEnvironment.ThrowOnConsentRead = true;
        var readOutcome = CreateInvocation(readEnvironment).Run(
            new[] { MotionStopTrialVocabulary.ModeToken, "A" });
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentConsumptionFailed, readOutcome.RefusalCode);
        Assert.Single(readEnvironment.Reads);
        Assert.Single(readEnvironment.Removals);

        var removeEnvironment = ValidEnvironment();
        removeEnvironment.ThrowOnConsentRemoval = true;
        var removeOutcome = CreateInvocation(removeEnvironment).Run(
            new[] { MotionStopTrialVocabulary.ModeToken, "A" });
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentConsumptionFailed, removeOutcome.RefusalCode);
        Assert.Single(removeEnvironment.Reads);
        Assert.Single(removeEnvironment.Removals);
    }

    [Theory]
    [MemberData(nameof(InvalidInvocationShapes))]
    public void Invalid_Invocation_Consumes_Consent_But_Does_Not_Read_Native_Or_Endpoint_Configuration(
        string[]? arguments)
    {
        var environment = ValidEnvironment();
        var gateACalls = 0;

        var outcome = CreateInvocation(environment, _ =>
        {
            gateACalls++;
            return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
        }).Run(arguments);

        Assert.Equal(MotionStopTrialVocabulary.InvalidInvocation, outcome.RefusalCode);
        Assert.Equal(4, outcome.ExitCode);
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentAccepted, outcome.ConsentCode);
        Assert.Equal(new[] { MotionStopTrialVocabulary.ConsentVariable }, environment.Removals);
        Assert.DoesNotContain(MotionLibraryProbeVocabulary.NativeDirectoryVariable, environment.Reads);
        Assert.DoesNotContain(MotionControllerEndpointResolver.EndpointVariable, environment.Reads);
        Assert.Equal(0, gateACalls);
    }

    public static IEnumerable<object[]> InvalidEndpoints()
    {
        yield return new object[] { null!, MotionControllerIdentityVocabulary.EndpointNotConfigured };
        yield return new object[] { SyntheticAddress + ":5000", MotionControllerIdentityVocabulary.EndpointRejected };
        yield return new object[] { "127.0.0.1", MotionControllerIdentityVocabulary.EndpointRejected };
    }

    [Theory]
    [MemberData(nameof(InvalidEndpoints))]
    public void Missing_Or_Malformed_Endpoint_Refuses_Before_Gate_A_Or_Controller_Kernel(
        string? endpoint,
        string expectedCode)
    {
        var environment = ValidEnvironment();
        if (endpoint is null)
        {
            environment.Values.Remove(MotionControllerEndpointResolver.EndpointVariable);
        }
        else
        {
            environment.Values[MotionControllerEndpointResolver.EndpointVariable] = endpoint;
        }

        var gateACalls = 0;
        var controllerKernelCreates = 0;
        var outcome = CreateInvocation(
            environment,
            _ =>
            {
                gateACalls++;
                return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
            },
            () => controllerKernelCreates++).Run(
                new[] { MotionStopTrialVocabulary.ModeToken, "A" });

        Assert.Equal(expectedCode, outcome.EndpointCode);
        Assert.Equal(expectedCode, outcome.RefusalCode);
        Assert.Equal(MotionLibraryProbeVocabulary.ConfigurationRedacted, outcome.NativeConfigurationCode);
        Assert.Equal(0, gateACalls);
        Assert.Equal(0, controllerKernelCreates);
        if (endpoint is not null)
        {
            Assert.DoesNotContain(endpoint, VisibleReport(outcome), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Missing_Or_Malformed_Native_Configuration_Refuses_Before_Endpoint_Or_Gate_A()
    {
        var missing = ValidEnvironment();
        missing.Values.Remove(MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable);
        var missingGateCalls = 0;
        var missingOutcome = CreateInvocation(missing, _ =>
        {
            missingGateCalls++;
            return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
        }).Run(new[] { MotionStopTrialVocabulary.ModeToken, "A" });

        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirNotConfigured, missingOutcome.RefusalCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirNotConfigured, missingOutcome.NativeConfigurationCode);
        Assert.Equal(0, missingGateCalls);
        Assert.DoesNotContain(MotionControllerEndpointResolver.EndpointVariable, missing.Reads);

        var malformed = ValidEnvironment();
        malformed.Values[MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable] = "not-a-hash";
        var malformedGateCalls = 0;
        var malformedOutcome = CreateInvocation(malformed, _ =>
        {
            malformedGateCalls++;
            return MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
        }).Run(new[] { MotionStopTrialVocabulary.ModeToken, "B" });

        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirRejected, malformedOutcome.RefusalCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibDirRejected, malformedOutcome.NativeConfigurationCode);
        Assert.Equal(0, malformedGateCalls);
        Assert.DoesNotContain(MotionControllerEndpointResolver.EndpointVariable, malformed.Reads);
    }

    [Fact]
    public void A_Second_Invocation_On_The_Same_Harness_Instance_Never_ReReads_Consent()
    {
        var environment = ValidEnvironment();
        var gateACalls = 0;
        var invocation = CreateInvocation(environment, _ =>
        {
            gateACalls++;
            return MotionLibraryProbeOutcome.Refused(
                MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
                "synthetic refusal",
                nativeHandlesReleased: true);
        });

        var first = invocation.Run(new[] { MotionStopTrialVocabulary.ModeToken, "A" });
        var second = invocation.Run(new[] { MotionStopTrialVocabulary.ModeToken, "B" });

        Assert.Equal(MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform, first.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.TrialAlreadyRun, second.RefusalCode);
        Assert.Single(environment.Removals);
        Assert.Single(environment.Reads, name => string.Equals(
            name,
            MotionStopTrialVocabulary.ConsentVariable,
            StringComparison.Ordinal));
        Assert.Equal(1, gateACalls);
    }
}
