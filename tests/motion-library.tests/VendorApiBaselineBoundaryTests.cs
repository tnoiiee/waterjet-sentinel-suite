using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Document-boundary coverage of the committed vendor API baseline
/// (<c>docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md</c>) and of the code properties that
/// document claims. These tests are static: they read committed files only, build nothing, contact
/// no controller and open no library.
///
/// They exist so the closeout record cannot silently lose a banner, a layer separation, a registry
/// status, the intake protocol, the misuse rule, the redaction posture or the closed gate, and so a
/// future change cannot reintroduce an option switch into the open address, an executable C1
/// candidate, a raw command surface or a leaked endpoint.
/// </summary>
public sealed class VendorApiBaselineBoundaryTests
{
    private const string BaselineRelativePath = "docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md";

    private static readonly string[] AuthorityBanners =
    {
        "VENDOR-DERIVED API REFERENCE",
        "NOT A REPLACEMENT FOR VENDOR DOCUMENTATION",
        "NOT ROADMAP AUTHORITY",
        "NOT EXECUTABLE AUTHORITY BY ITSELF",
        "NOT HARDWARE AUTHORIZATION",
        "NOT MOTION AUTHORIZATION",
        "NOT OUTPUT-CONTROL AUTHORIZATION",
    };

    private static readonly string[] RegistryStatuses =
    {
        "VERIFIED_FOR_WJSS",
        "REVIEWED_NOT_USED",
        "DEPRECATED_NOT_ALLOWED",
        "BLOCKED_PENDING_VENDOR_EVIDENCE",
        "NOT_REVIEWED",
    };

    private static readonly string[] ResultEnumOrder =
    {
        "GCLIB_SUCCESS",
        "GCLIB_INVALID_ARGUMENT",
        "GCLIB_INTERNAL_ERROR",
        "GCLIB_TIMEOUT",
        "GCLIB_COMMAND_ERROR",
        "GCLIB_BUFFER_TOO_SMALL",
        "GCLIB_NOT_CONNECTED",
        "GCLIB_NOT_SUBSCRIBED",
    };

    private static readonly string[] VerifiedContractEntries =
    {
        "gclib_force_gcaps",
        "gclib_open",
        "gclib_close",
        "gclib_address",
        "gclib_revision_information",
        "gclib_serial_number",
        "gclib_command",
        "gclib_error",
    };

    private static readonly string[] C1Candidates =
    {
        "ST",
        "JG",
        "BG",
        "HV",
        "HM",
        "AC",
        "SP",
        "DC",
        "PA",
        "DP=0",
        "DE=0",
        "SB",
        "CB",
    };

    private static readonly string[] DocumentsThatMustPointAtTheBaseline =
    {
        "docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md",
        "docs/MOTION_OBSERVED_STATE_FOUNDATION.md",
        "docs/MOTION_IDENTITY_FIRST_CONTACT_RUNBOOK.md",
        "docs/MOTION_OBSERVATION_TRIAL_RUNBOOK.md",
        "adapters/motion-library/README.md",
        "apps/motion-identity/README.md",
        "apps/motion-observation/README.md",
        "CHANGELOG.md",
    };

    private static readonly string[] OpenCallSiteFiles =
    {
        "adapters/motion-library/MotionControllerIdentitySession.cs",
        "adapters/motion-library/MotionObservationTrialSession.cs",
    };

    private static readonly string[] CommandCapableAdapterFiles =
    {
        "MotionControllerCommandKernel.cs",
        "MotionControllerApi.cs",
        "MotionObservationTrialSession.cs",
        "MotionControllerIdentitySession.cs",
    };

    private static readonly string[] ProhibitedEntries =
    {
        "gclib_set_" + "output",
        "gclib_digital_" + "output",
        "gclib_motion_" + "complete",
        "gclib_" + "program",
        "gclib_set_" + "baud",
    };

    private static readonly string[] ExpectedCommandExportCarrierFiles =
    {
        "MotionControllerCommandKernel.cs",
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Baseline() => ReadRepositoryFile(BaselineRelativePath);

    private static int CountOccurrences(string text, string token) =>
        text.Split(token, StringSplitOptions.None).Length - 1;

    /// <summary>
    /// Returns the text between two markers, exclusive of the end marker, so an assertion can be
    /// scoped to one evidence record instead of the whole document.
    /// </summary>
    private static string Section(string document, string startMarker, string endMarker)
    {
        var start = document.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing section marker: {startMarker}");

        var end = document.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker after '{startMarker}': {endMarker}");

        return document[start..end];
    }

    // --------------------------------------------------------------------- existence and banner

    [Fact]
    public void The_Vendor_Api_Baseline_Exists_At_The_Canonical_Evidence_Path()
    {
        Assert.True(File.Exists(
            Path.Combine(RepoRoot(), BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void The_Baseline_Carries_Every_Authority_Banner()
    {
        var document = Baseline();

        foreach (var banner in AuthorityBanners)
        {
            Assert.Contains(banner, document, StringComparison.Ordinal);
        }

        Assert.Contains("EVIDENCE ONLY", document, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------------------- provenance

    [Fact]
    public void The_Baseline_Records_Its_Provenance_And_The_Exact_Documented_Version()
    {
        var document = Baseline();

        Assert.Contains("`gclib.pdf`", document, StringComparison.Ordinal);
        Assert.Contains("2.6.0", document, StringComparison.Ordinal);
        Assert.Contains("355", document, StringComparison.Ordinal);
        Assert.Contains("Google Drive", document, StringComparison.Ordinal);
        Assert.Contains("read-only", document, StringComparison.Ordinal);
        Assert.Contains("| Committed to this repository | no |", document, StringComparison.Ordinal);
        Assert.Contains("deleted after extraction", document, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Baseline_Cites_Printed_Pages_And_Keeps_The_Page_Convention_Explicit()
    {
        var document = Baseline();

        Assert.Contains("printed page", document, StringComparison.Ordinal);
        Assert.Contains("page index minus 34", document, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(document, "printed page") >= 10,
            "Every vendor claim must carry a printed-page citation.");
        Assert.Contains("reproduces no dotted section numeral", document, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Baseline_Carries_No_Address_Serial_Path_Hash_Or_Document_Bytes()
    {
        var document = Baseline();

        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", document);
        Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", document);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", document);
        Assert.DoesNotContain("192.168", document, StringComparison.Ordinal);
        Assert.DoesNotContain("%PDF", document, StringComparison.Ordinal);
        Assert.DoesNotContain(".dll", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Serial:", document, StringComparison.Ordinal);
        Assert.DoesNotContain("\\\\", document, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------ layer separation

    [Fact]
    public void The_Baseline_Keeps_The_Three_Interface_Layers_Separate()
    {
        var document = Baseline();

        Assert.Contains("Layer 1", document, StringComparison.Ordinal);
        Assert.Contains("Layer 2", document, StringComparison.Ordinal);
        Assert.Contains("Layer 3", document, StringComparison.Ordinal);
        Assert.Contains(
            "CURRENT C API | DEPRECATED LEGACY API | CONTROLLER COMMAND LANGUAGE",
            document,
            StringComparison.Ordinal);

        // The controller command language is explicitly NOT documented by the library manual.
        Assert.Contains("Not documented in this manual", document, StringComparison.Ordinal);
        Assert.Contains("printed pages 75-76", document, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------ the open contract

    [Fact]
    public void The_Open_Address_Is_The_Validated_Address_And_Nothing_Else()
    {
        var document = Baseline();
        var apiSource = ReadRepositoryFile("adapters/motion-library/MotionControllerApi.cs");

        Assert.Contains("validated controller address and nothing", document, StringComparison.Ordinal);
        Assert.Contains("return configuration.EndpointValue;", apiSource, StringComparison.Ordinal);

        foreach (var fileName in OpenCallSiteFiles)
        {
            var session = ReadRepositoryFile(fileName);

            Assert.Equal(1, CountOccurrences(session, "MotionControllerApi.BuildOpenAddress("));
            Assert.Equal(1, CountOccurrences(session, "kernel.InvokeOpen("));
        }
    }

    [Fact]
    public void The_Legacy_Option_Switches_Are_Attributed_Only_To_The_Deprecated_Layer()
    {
        var document = Baseline();

        Assert.Contains("**only** option-switch table", document, StringComparison.Ordinal);
        Assert.Contains("never** evidence about the current open entry", document, StringComparison.Ordinal);
        Assert.Contains("DEPRECATED_NOT_ALLOWED", document, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Force_Direct_Selection_Precedes_The_Open_And_Is_Documented_As_Irreversible()
    {
        var document = Baseline();

        Assert.Contains("cannot be undone", document, StringComparison.Ordinal);

        foreach (var fileName in OpenCallSiteFiles)
        {
            var session = ReadRepositoryFile(fileName);
            var force = session.IndexOf("InvokeForceDirectMode", StringComparison.Ordinal);
            var open = session.IndexOf("kernel.InvokeOpen(", StringComparison.Ordinal);

            Assert.True(force >= 0, $"{fileName} must select direct mode explicitly.");
            Assert.True(open >= 0, $"{fileName} must open exactly one handle.");
            Assert.True(force < open, $"{fileName} must select direct mode before the open.");
            Assert.Equal(1, CountOccurrences(session, "InvokeForceDirectMode("));
            Assert.Contains("directOnly: 0", session, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------------------ the registry

    [Fact]
    public void The_Registry_Carries_The_Five_Statuses_And_Defaults_To_Not_Reviewed()
    {
        var document = Baseline();

        foreach (var status in RegistryStatuses)
        {
            Assert.Contains(status, document, StringComparison.Ordinal);
        }

        Assert.Contains("**The default.**", document, StringComparison.Ordinal);
        Assert.Contains("Absence is not permission.", document, StringComparison.Ordinal);
        Assert.Contains("`NOT_REVIEWED` means", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Verified_Contract_Is_Registered_With_A_Permitting_Or_Reviewed_Status()
    {
        var lines = Baseline().Split('\n');

        foreach (var entry in VerifiedContractEntries)
        {
            var rows = lines
                .Where(line => line.StartsWith($"| `{entry}` |", StringComparison.Ordinal))
                .ToArray();

            Assert.NotEmpty(rows);
            Assert.Contains(
                rows,
                row => RegistryStatuses.Any(status => row.Contains(status, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void The_Result_Enumeration_Is_Recorded_In_The_Documented_Order()
    {
        var document = Baseline();
        var previous = -1;

        foreach (var name in ResultEnumOrder)
        {
            var index = document.IndexOf($"| `{name}` |", StringComparison.Ordinal);

            Assert.True(index > previous, $"{name} must appear after the previous enumerator.");
            previous = index;
        }
    }

    // ------------------------------------------------------------------------ intake protocol

    [Fact]
    public void The_Intake_Protocol_Has_Exactly_Fifteen_Mandatory_Steps()
    {
        var stepRows = Baseline()
            .Split('\n')
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal)
                           && line.Contains("| **", StringComparison.Ordinal)
                           && char.IsDigit(line[2]))
            .ToArray();

        Assert.Equal(15, stepRows.Length);
        Assert.Contains("Name the layer.", Baseline(), StringComparison.Ordinal);
        Assert.Contains("Prove the request is a literal.", Baseline(), StringComparison.Ordinal);
        Assert.Contains("Update atomically.", Baseline(), StringComparison.Ordinal);
        Assert.Contains("### G.3 Prohibitions", Baseline(), StringComparison.Ordinal);
        Assert.Contains("No entry by chat memory", Baseline(), StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------- hardware records

    [Fact]
    public void The_Identity_First_Contact_Record_Separates_Observation_Classification_Confirmation_And_Acceptance()
    {
        var document = Baseline();
        var identityRecord = Section(document, "## H. B1-H", "## I. B2-H");
        var observedRecord = Section(identityRecord, "### H.1 Harness observation", "### H.4");

        // B1-H remains a PASS, and the expected model remains the committed token.
        Assert.Contains("## H. B1-H — identity first contact: PASS", identityRecord, StringComparison.Ordinal);
        Assert.Contains("| B1-H status in this baseline | **PASS** |", identityRecord, StringComparison.Ordinal);
        Assert.Contains(
            MotionControllerIdentityVocabulary.ExpectedControllerModel,
            identityRecord,
            StringComparison.Ordinal);

        // The four records exist as separate fields: harness observation, harness classification,
        // Owner confirmation, final Owner acceptance.
        Assert.Contains("### H.1 Harness observation", identityRecord, StringComparison.Ordinal);
        Assert.Contains("### H.2 Harness classification", identityRecord, StringComparison.Ordinal);
        Assert.Contains("| OWNER CONFIRMATION |", identityRecord, StringComparison.Ordinal);
        Assert.Contains("| FINAL OWNER ACCEPTANCE |", identityRecord, StringComparison.Ordinal);

        // The observation is the revision text that was actually observed, and the classification
        // is the manual-confirmation path that text produces.
        Assert.Contains("`DMCB140 Rev 1.0c2`", observedRecord, StringComparison.Ordinal);
        Assert.Contains(
            "| Exact expected token present in the observed revision text | NO |",
            observedRecord,
            StringComparison.Ordinal);
        Assert.Contains(
            $"| Model comparison | `{MotionControllerIdentityVocabulary.ModelConfirmationRequired}` |",
            observedRecord,
            StringComparison.Ordinal);
        Assert.Contains(
            $"| Outcome code | `{MotionControllerIdentityVocabulary.ModelConfirmationRequired}` |",
            observedRecord,
            StringComparison.Ordinal);
        Assert.Contains("| Harness exit | 2", observedRecord, StringComparison.Ordinal);

        // The observed record never claims the automatic path and never claims exit 0.
        Assert.DoesNotContain(
            MotionControllerIdentityVocabulary.ModelMatched,
            observedRecord,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            MotionControllerIdentityVocabulary.IdentitySessionComplete,
            observedRecord,
            StringComparison.Ordinal);
        Assert.DoesNotContain("exit 0", observedRecord, StringComparison.Ordinal);

        // Cleanup, redaction and the no-command posture stay recorded on the observed record.
        Assert.Contains("| Controller handle closed | TRUE", observedRecord, StringComparison.Ordinal);
        Assert.Contains("| Native modules released | TRUE", observedRecord, StringComparison.Ordinal);
        Assert.Contains(
            MotionControllerIdentityVocabulary.SerialObservedRedacted,
            observedRecord,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Motion or output-control command | NONE",
            observedRecord,
            StringComparison.Ordinal);
        Assert.Contains("The Agent did not run, repeat or", identityRecord, StringComparison.Ordinal);

        // No endpoint, serial value, path or hash on the identity record.
        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", identityRecord);
        Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{64}\b", identityRecord);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", identityRecord);
    }

    [Fact]
    public void The_Automatic_Model_Match_Is_Described_Only_As_A_Separate_Software_Path()
    {
        var document = Baseline();
        var identityRecord = Section(document, "## H. B1-H", "## I. B2-H");
        var automaticPath = Section(
            identityRecord,
            "### H.4 The automatic software path",
            "### H.5");

        Assert.Contains(MotionControllerIdentityVocabulary.ModelMatched, automaticPath, StringComparison.Ordinal);
        Assert.Contains(
            MotionControllerIdentityVocabulary.IdentitySessionComplete,
            automaticPath,
            StringComparison.Ordinal);
        Assert.Contains("exit 0", automaticPath, StringComparison.Ordinal);
        Assert.Contains("**not** what this hardware", automaticPath, StringComparison.Ordinal);
        Assert.Contains(
            MotionControllerIdentityVocabulary.OwnerManualConfirmationRequired,
            automaticPath,
            StringComparison.Ordinal);

        // The software path is still documented, so this is a separation and not a deletion.
        Assert.Contains(MotionControllerIdentityVocabulary.ModelMatched, document, StringComparison.Ordinal);
        Assert.Contains(
            MotionControllerIdentityVocabulary.IdentitySessionComplete,
            document,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_Stale_Observed_State_Comments_Are_Recorded_As_A_Punchlist_Without_A_Source_Change()
    {
        var document = Baseline();
        var punchlist = Section(
            document,
            "### K.5 Documentation punchlist",
            "## L. Validation position");

        Assert.Contains("MotionObservedState.cs", punchlist, StringComparison.Ordinal);
        Assert.Contains("InterpretationState", punchlist, StringComparison.Ordinal);
        Assert.Contains("Unavailable", punchlist, StringComparison.Ordinal);
        Assert.Contains("**not** made in this pass", punchlist, StringComparison.Ordinal);
        Assert.Contains("PR #32", punchlist, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Observation_Record_Is_A_Pass_With_Sixteen_Accepted_Reads_And_Proven_Cleanup()
    {
        var document = Baseline();

        Assert.Contains("## I. B2-H — read-only observed-state trial: PASS", document, StringComparison.Ordinal);
        Assert.Contains("16 of 16", document, StringComparison.Ordinal);
        Assert.Contains(MotionObservationTrialVocabulary.TrialComplete, document, StringComparison.Ordinal);
        Assert.Contains(MotionObservationTrialVocabulary.OpenSucceeded, document, StringComparison.Ordinal);

        var readingRows = document
            .Split('\n')
            .Where(line => line.Contains("| `AXIS_STATUS` |", StringComparison.Ordinal)
                           || line.Contains("| `COMMAND_POSITION` |", StringComparison.Ordinal)
                           || line.Contains("| `ACTUAL_POSITION` |", StringComparison.Ordinal)
                           || line.Contains("| `DIGITAL_OUTPUT` |", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(16, readingRows.Length);
        Assert.All(readingRows, row => Assert.Contains("| `OBSERVATION_", row, StringComparison.Ordinal));

        Assert.Contains("| Observation boundary invalidated | TRUE", document, StringComparison.Ordinal);
        Assert.Contains("| Controller handle closed | TRUE", document, StringComparison.Ordinal);
        Assert.Contains("| Native handles released | TRUE", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Observed_Value_Keeps_A_Weak_Interpretation()
    {
        var document = Baseline();
        var readingRows = document
            .Split('\n')
            .Where(line => line.Contains("| `AXIS_STATUS` |", StringComparison.Ordinal)
                           || line.Contains("| `COMMAND_POSITION` |", StringComparison.Ordinal)
                           || line.Contains("| `ACTUAL_POSITION` |", StringComparison.Ordinal)
                           || line.Contains("| `DIGITAL_OUTPUT` |", StringComparison.Ordinal))
            .ToArray();

        var statusRows = readingRows.Where(row => row.Contains("| `AXIS_STATUS` |", StringComparison.Ordinal)).ToArray();
        var positionRows = readingRows
            .Where(row => row.Contains("| `COMMAND_POSITION` |", StringComparison.Ordinal)
                          || row.Contains("| `ACTUAL_POSITION` |", StringComparison.Ordinal))
            .ToArray();
        var outputRows = readingRows.Where(row => row.Contains("| `DIGITAL_OUTPUT` |", StringComparison.Ordinal)).ToArray();

        Assert.All(statusRows, row =>
            Assert.Contains(MotionObservedStateVocabulary.InterpretationUnconfirmed, row, StringComparison.Ordinal));
        Assert.All(positionRows, row =>
            Assert.Contains(MotionObservedStateVocabulary.InterpretationRawObserved, row, StringComparison.Ordinal));
        Assert.All(outputRows, row =>
            Assert.Contains(MotionObservedStateVocabulary.InterpretationUnconfirmed, row, StringComparison.Ordinal));
        Assert.All(readingRows, row =>
            Assert.DoesNotContain(
                $"| {MotionObservedStateVocabulary.InterpretationVerified} |",
                row,
                StringComparison.Ordinal));
    }

    [Fact]
    public void The_Baseline_Forbids_Every_Interpretation_The_Values_Cannot_Support()
    {
        var document = Baseline();

        Assert.Contains("No bit meaning.", document, StringComparison.Ordinal);
        Assert.Contains("No limit, home or fault state.", document, StringComparison.Ordinal);
        Assert.Contains("No motion completion.", document, StringComparison.Ordinal);
        Assert.Contains("No brake state.", document, StringComparison.Ordinal);
        Assert.Contains("No engineering unit.", document, StringComparison.Ordinal);
        Assert.Contains("No physical meaning for `TP` or `RP`.", document, StringComparison.Ordinal);
        Assert.Contains("No safe state.", document, StringComparison.Ordinal);
        Assert.Contains("No authorization.", document, StringComparison.Ordinal);
    }

    // --------------------------------------------------------------------------------- C1 and gate

    [Fact]
    public void Every_C1_Candidate_Is_Registered_As_Blocked_And_Has_No_Executable_Form()
    {
        var lines = Baseline().Split('\n');

        foreach (var candidate in C1Candidates)
        {
            var row = lines.FirstOrDefault(line =>
                line.StartsWith($"| `{candidate}` |", StringComparison.Ordinal));

            Assert.NotNull(row);
            Assert.Contains("BLOCKED_PENDING_VENDOR_EVIDENCE", row, StringComparison.Ordinal);
        }

        var plan = ReadRepositoryFile("adapters/motion-library/MotionObservationCommandPlan.cs");
        Assert.Contains("private static readonly ReadOnlyDictionary<MotionObservationIntent, string> RequestTemplates", plan, StringComparison.Ordinal);
        Assert.Equal(16, MotionObservationCommandPlan.Templates.Count);
        Assert.Equal(16, MotionObservationCommandPlan.Intents.Count);

        foreach (var template in MotionObservationCommandPlan.Templates.Values)
        {
            Assert.Matches(@"^(MG _TS[A-D]|TP [A-D]|RP [A-D]|MG @OUT\[[1-4]\])$", template);
        }
    }

    [Fact]
    public void The_Motion_Gate_Stays_Closed_And_The_Candidate_Values_Stay_Uncommissioned()
    {
        var document = Baseline();
        var contracts = ReadRepositoryFile("packages/contracts/MotionCommissioning.cs");
        var gate = ReadRepositoryFile("packages/application/Motion/MotionCommandGate.cs");

        Assert.Contains("CLOSED", document, StringComparison.Ordinal);
        Assert.Contains("MOTION_COMMISSIONING_GATE_NOT_OPEN", contracts, StringComparison.Ordinal);
        Assert.Contains("\"NOT_CONFIGURED\"", contracts, StringComparison.Ordinal);
        Assert.Contains("\"NOT_COMMISSIONED\"", contracts, StringComparison.Ordinal);
        Assert.Contains("IsClosed => true", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void No_C1_Implementation_Or_Write_Path_Exists_In_The_Adapter()
    {
        foreach (var fileName in CommandCapableAdapterFiles)
        {
            var source = ReadRepositoryFile($"adapters/motion-library/{fileName}");

            foreach (var prohibited in ProhibitedEntries)
            {
                Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
            }
        }

        // The single scoped command export is the ONLY command-capable name in the adapter, and it
        // appears in exactly one file.
        var adapterDirectory = Path.Combine(RepoRoot(), "adapters", "motion-library");
        var carrierFiles = Directory
            .EnumerateFiles(adapterDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => File.ReadAllText(file).Contains(
                MotionControllerCommandApi.CommandExportName,
                StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCommandExportCarrierFiles, carrierFiles);
    }

    // --------------------------------------------------------------------- pointer and traceability

    [Fact]
    public void Every_Current_Facing_Document_Points_At_The_Baseline()
    {
        foreach (var relativePath in DocumentsThatMustPointAtTheBaseline)
        {
            var text = ReadRepositoryFile(relativePath);

            Assert.Contains("GCLIB_2_6_0_API_USAGE_BASELINE.md", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Baseline_Declares_PR32_As_The_C1_Successor_And_Not_Implemented()
    {
        var document = Baseline();

        Assert.Contains("## K. PR #32 — C1 successor boundary (documented, NOT IMPLEMENTED)", document, StringComparison.Ordinal);
        Assert.Contains("**NOT IMPLEMENTED.**", document, StringComparison.Ordinal);
        Assert.Contains("MotionCommandGate.IsClosed` remains `true`", document, StringComparison.Ordinal);
    }
}
