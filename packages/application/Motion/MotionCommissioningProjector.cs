using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Wjss.Contracts;

namespace Wjss.Runtime.Core.Motion;

/// <summary>
/// Composes the STATIC read-only motion commissioning catalogue: the Owner-confirmed topology, the
/// standing configuration and commissioning statuses, the provenance, the evidence notes, the
/// candidate values and the authorization gate.
///
/// The catalogue is NOT part of the Runtime Snapshot and NOT part of a Runtime Delta: it does not
/// change with a revision, so it is served by the dedicated read-only catalogue route instead of
/// being re-sent with every Snapshot.
///
/// The catalogue is a pure function of the Owner-confirmed topology catalog and of the standing
/// not-configured / not-commissioned state of this software revision: it reads no device, contacts
/// no controller and depends on no clock, so the same revision always produces the same catalogue,
/// the same canonical text and the same fingerprint.
///
/// Nothing here grants authority. The gate is CLOSED, every axis refuses an absolute Move, no
/// parameter is executable, and no member carries an endpoint.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class MotionCommissioningProjector
{
    /// <summary>The three installed limit inputs of an axis, in Reverse, Home, Forward order.</summary>
    private static readonly string[] LimitSignalRoles =
    [
        MotionVocabulary.SignalReverse,
        MotionVocabulary.SignalHome,
        MotionVocabulary.SignalForward,
    ];

    /// <summary>Composes the catalogue of this revision.</summary>
    public static MotionCommissioningCatalogue Project()
    {
        var controllers = MotionTopologyCatalog.Controllers;
        var controllerProjections = new MotionControllerProjection[controllers.Count];
        for (var index = 0; index < controllers.Count; index++)
        {
            controllerProjections[index] = new MotionControllerProjection
            {
                Identity = controllers[index],
                Configuration = ControllerConfiguration(),
            };
        }

        var axes = MotionTopologyCatalog.Axes;
        var axisProjections = new MotionAxisProjection[axes.Count];
        for (var index = 0; index < axes.Count; index++)
        {
            axisProjections[index] = new MotionAxisProjection
            {
                Identity = axes[index],
                State = AxisState(axes[index]),
            };
        }

        var catalogue = new MotionCommissioningCatalogue
        {
            SectionId = MotionVocabulary.SectionId,
            SectionRevision = MotionVocabulary.SectionRevision,
            Source = MotionVocabulary.Source,
            MotionTopologyRevision = MotionVocabulary.MotionTopologyRevision,
            AuthorizationGateState = MotionAuthorizationGate.State,
            AuthorizationGateReasons = MotionVocabulary.GateRefusalReasons,
            MotionIntents = MotionVocabulary.MotionIntents,
            AllowedMotionStates = Array.Empty<string>(),
            SessionState = MotionVocabulary.SessionStateNoSession,
            CommandTotals = new MotionCommandTotals
            {
                Evaluated = 0,
                Authorized = 0,
                Refused = 0,
                HardwareCommandsIssued = 0,
                CommandState = MotionVocabulary.CommandTotalsStateNoCommands,
                JournalPersistence = MotionVocabulary.JournalPersistenceNone,
            },
            Controllers = Array.AsReadOnly(controllerProjections),
            Axes = Array.AsReadOnly(axisProjections),
            WaterJets = MotionTopologyCatalog.WaterJetBindings,
            MappingEvidenceNotes = MotionTopologyCatalog.MappingEvidenceNotes,
            JogSpeed = new MotionParameterState
            {
                ParameterId = "JOG_SPEED",
                DisplayValue = MotionVocabulary.JogSpeedDisplayValue,
                Unit = MotionVocabulary.SpeedUnitControllerUnits,
                Status = MotionVocabulary.ParameterStatusUnconfirmed,
                ProvenanceBasis = MotionVocabulary.ParameterStatusUnconfirmed,
                Executable = false,
            },
            NotVerifiedClaims = MotionVocabulary.NotVerifiedClaims,
            StatusLegend = StatusLegend(),
            CanonicalText = string.Empty,
            Fingerprint = string.Empty,
        };

        var canonicalText = CanonicalText(catalogue);
        return catalogue with
        {
            CanonicalText = canonicalText,
            Fingerprint = Fingerprint(canonicalText),
        };
    }

    /// <summary>
    /// The whole catalogue as one deterministic text block. This is the parity surface of the
    /// motion foundation: any change to a published value changes this text and its fingerprint.
    /// </summary>
    public static string CanonicalText(MotionCommissioningCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        var text = new StringBuilder();
        text.Append("MOTION COMMISSIONING FOUNDATION rev ").Append(catalogue.SectionRevision)
            .Append(" (").Append(catalogue.MotionTopologyRevision).Append(") - ")
            .Append(catalogue.Source).Append('\n');
        text.Append("GATE ").Append(catalogue.AuthorizationGateState).Append(" - ")
            .Append(string.Join(", ", catalogue.AuthorizationGateReasons)).Append('\n');
        text.Append("SESSION ").Append(catalogue.SessionState).Append(" - EVALUATED ")
            .Append(catalogue.CommandTotals.Evaluated).Append(" AUTHORIZED ")
            .Append(catalogue.CommandTotals.Authorized).Append(" REFUSED ")
            .Append(catalogue.CommandTotals.Refused).Append(" HARDWARE ISSUED ")
            .Append(catalogue.CommandTotals.HardwareCommandsIssued).Append(" - JOURNAL ")
            .Append(catalogue.CommandTotals.JournalPersistence).Append('\n');
        text.Append("JOG SPEED ").Append(catalogue.JogSpeed.DisplayValue).Append(' ')
            .Append(catalogue.JogSpeed.Unit).Append(" - ").Append(catalogue.JogSpeed.Status)
            .Append(" - EXECUTABLE ").Append(catalogue.JogSpeed.Executable ? "true" : "false").Append('\n');
        text.Append("INTENTS ").Append(string.Join(", ", catalogue.MotionIntents)).Append('\n');
        text.Append("CONTROLLERS ").Append(catalogue.Controllers.Count).Append('\n');
        for (var index = 0; index < catalogue.Controllers.Count; index++)
        {
            var controller = catalogue.Controllers[index];
            text.Append('C').Append(index + 1).Append(' ').Append(controller.Identity.ControllerId)
                .Append(" cabinet ").Append(controller.Identity.CabinetId)
                .Append(" axes ").Append(controller.Identity.AxisCount)
                .Append(" model ").Append(controller.Identity.ControllerModel)
                .Append(" (").Append(controller.Identity.ControllerModelStatus).Append(')')
                .Append(" identity ").Append(controller.Identity.ControllerIdentityBasis)
                .Append(" port ").Append(controller.Configuration.MotionPortIdentity)
                .Append(" (").Append(controller.Configuration.MotionPortStatus).Append(')')
                .Append(" endpoint ").Append(controller.Configuration.EndpointIdentity)
                .Append(" config ").Append(controller.Configuration.ControllerConfigurationStatus)
                .Append(" library ").Append(controller.Configuration.LibraryInstallationStatus)
                .Append(" communication ").Append(controller.Configuration.CommunicationState)
                .Append(" dispatch ready ").Append(controller.Configuration.MotionDispatchReady ? "true" : "false")
                .Append(" blocked ").Append(controller.Configuration.MotionDispatchBlockedReason)
                .Append(" parameters ").Append(controller.Configuration.ParameterConfigurationState)
                .Append(" software limits ").Append(controller.Configuration.SoftwareLimitConfigurationState)
                .Append(" polarity ").Append(controller.Configuration.LimitInputPolarityStatus)
                .Append(" endstops ").Append(controller.Configuration.EndstopConfigurationStatus)
                .Append('\n');
        }

        text.Append("AXES ").Append(catalogue.Axes.Count).Append('\n');
        for (var index = 0; index < catalogue.Axes.Count; index++)
        {
            var axis = catalogue.Axes[index];
            var state = axis.State;
            text.Append('A').Append(index + 1).Append(' ').Append(axis.Identity.ControllerId)
                .Append('/').Append(axis.Identity.AxisId).Append(' ')
                .Append(axis.Identity.AxisRole).Append(" (").Append(axis.Identity.AxisRoleBasis).Append(')')
                .Append(" sense ").Append(axis.Identity.MotionSenseKind)
                .Append(" (").Append(axis.Identity.MotionSenseBasis).Append(')')
                .Append(" brake ").Append(axis.Identity.BrakeOutputIdentity)
                .Append(" (").Append(axis.Identity.BrakeOutputMappingBasis).Append(')')
                .Append(" commissioning ").Append(state.CommissioningState)
                .Append(" home ").Append(state.HomingState).Append(" (").Append(state.HomingStateStatus).Append(')')
                .Append(" brake command ").Append(state.BrakeCommandState)
                .Append(" brake state ").Append(state.BrakeState).Append(" (").Append(state.BrakeStateStatus).Append(')')
                .Append(" motion ").Append(state.MotionState).Append(" (").Append(state.MotionStateStatus).Append(')')
                .Append(" position ").Append(state.Sense.PositionValueStatus)
                .Append(" unit ").Append(state.Kinematic.PublishedUnit)
                .Append(" conversion ").Append(state.Kinematic.KinematicConversionStatus)
                .Append(" signals ");
            for (var signal = 0; signal < state.LimitSignals.Count; signal++)
            {
                if (signal > 0) text.Append(" | ");
                var limit = state.LimitSignals[signal];
                text.Append(limit.SignalRole).Append(' ').Append(limit.SignalKind).Append(' ')
                    .Append(limit.PolarityStatus).Append(' ').Append(limit.SignalState);
            }

            text.Append(" move ").Append(state.MoveAuthorization.MoveAuthorizationState)
                .Append(" [").Append(string.Join(", ", state.MoveAuthorization.MoveRefusalReasons)).Append(']')
                .Append('\n');
        }

        text.Append("WATER JETS ").Append(catalogue.WaterJets.Count).Append('\n');
        for (var index = 0; index < catalogue.WaterJets.Count; index++)
        {
            var waterJet = catalogue.WaterJets[index];
            text.Append('W').Append(index + 1).Append(' ').Append(waterJet.WaterJetId)
                .Append(" cabinet ").Append(waterJet.CabinetId)
                .Append(" controller ").Append(waterJet.ControllerId)
                .Append(" horizontal ").Append(waterJet.HorizontalAxisId)
                .Append(" vertical ").Append(waterJet.VerticalAxisId)
                .Append(" - ").Append(waterJet.MappingStatus)
                .Append(" (").Append(waterJet.MappingBasis).Append(')').Append('\n');
        }

        text.Append("EVIDENCE NOTES ").Append(catalogue.MappingEvidenceNotes.Count).Append('\n');
        for (var index = 0; index < catalogue.MappingEvidenceNotes.Count; index++)
        {
            var note = catalogue.MappingEvidenceNotes[index];
            text.Append('E').Append(index + 1).Append(' ').Append(note.EvidenceId)
                .Append(" water jets ").Append(string.Join(",", note.WaterJetIds))
                .Append(" cabinets ").Append(string.Join(",", note.CabinetIds))
                .Append(" status ").Append(note.Status)
                .Append(" classification ").Append(note.Classification)
                .Append(" resolution ").Append(note.PhysicalResolution).Append('\n');
        }

        text.Append("NOT VERIFIED ").Append(catalogue.NotVerifiedClaims.Count).Append('\n');
        for (var index = 0; index < catalogue.NotVerifiedClaims.Count; index++)
        {
            text.Append('N').Append(index + 1).Append(' ').Append(catalogue.NotVerifiedClaims[index]).Append('\n');
        }

        text.Append("LEGEND ").Append(catalogue.StatusLegend.Count).Append('\n');
        for (var index = 0; index < catalogue.StatusLegend.Count; index++)
        {
            text.Append('L').Append(index + 1).Append(' ').Append(catalogue.StatusLegend[index]).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>SHA-256 of the canonical text, lowercase hex: the parity fingerprint.</summary>
    public static string Fingerprint(string canonicalText)
    {
        ArgumentNullException.ThrowIfNull(canonicalText);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText)));
    }

    /// <summary>
    /// Every status and refusal code with its wording, in a fixed order, so no surface can present a
    /// state by colour alone. The order is explicit: dictionary enumeration order is not contractual.
    /// </summary>
    private static ReadOnlyCollection<string> StatusLegend()
    {
        var codes = MotionVocabulary.StatusLegendOrder;
        var lines = new string[codes.Count];
        for (var index = 0; index < codes.Count; index++)
        {
            lines[index] = $"{codes[index]} - {MotionVocabulary.TextFor(codes[index])}";
        }

        return Array.AsReadOnly(lines);
    }

    /// <summary>
    /// The configuration state of one controller. Identical for all four controllers in this
    /// revision: no controller is configured, connected, commissioned or ready.
    /// </summary>
    private static MotionControllerConfigurationState ControllerConfiguration() => new()
    {
        MotionPortIdentity = MotionVocabulary.MotionPortNotConfigured,
        MotionPortStatus = MotionVocabulary.MotionPortStatusNotConfigured,
        EndpointIdentity = MotionVocabulary.EndpointNotConfigured,
        ControllerConfigurationStatus = MotionVocabulary.ControllerConfigurationNotConfigured,
        LibraryInstallationStatus = MotionVocabulary.GclibStatusNotInstalled,
        CommunicationState = MotionVocabulary.CommunicationNotConnected,
        MotionDispatchReady = false,
        ParameterConfigurationState = MotionVocabulary.StatusNotConfigured,
        SoftwareLimitConfigurationState = MotionVocabulary.StatusNotCommissioned,
        LimitInputPolarityStatus = MotionVocabulary.PolarityUnconfirmed,
        EndstopConfigurationStatus = MotionVocabulary.StatusNotCommissioned,
        MotionDispatchBlockedReason = MotionVocabulary.RefusalMotionCommissioningGateNotOpen,
    };

    /// <summary>The commissioning state of one axis: every member an explicit not-known value.</summary>
    private static MotionAxisCommissioningState AxisState(MotionAxisIdentity identity)
    {
        var signals = new MotionLimitSignalState[LimitSignalRoles.Length];
        for (var index = 0; index < LimitSignalRoles.Length; index++)
        {
            signals[index] = new MotionLimitSignalState
            {
                SignalRole = LimitSignalRoles[index],
                SignalKind = identity.MotionSenseKind,
                // The switches are installed and their behaviour on the existing hardware is real,
                // but the electrical meaning is only established by reading the Legacy implementation
                // and the Drawing against the installation. Until then the polarity is unconfirmed
                // and the signal state is unknown: it is never assumed active high or active low.
                PolarityStatus = MotionVocabulary.PolarityUnconfirmed,
                SignalState = MotionVocabulary.SignalStateUnknown,
                SignalStateStatus = MotionVocabulary.StatusNotAvailable,
                SenseBasis = MotionVocabulary.BasisOwnerConfirmed,
            };
        }

        return new MotionAxisCommissioningState
        {
            CommissioningState = MotionVocabulary.CommissioningNotCommissioned,
            HomingState = MotionVocabulary.HomingNotHomed,
            HomingStateStatus = MotionVocabulary.SignalStateUnknown,
            BrakeCommandState = MotionVocabulary.BrakeCommandNone,
            BrakeState = MotionVocabulary.BrakeStateUnknown,
            BrakeStateStatus = MotionVocabulary.StatusNotAvailable,
            MotionState = MotionVocabulary.MotionStateUnknown,
            MotionStateStatus = MotionVocabulary.StatusNotAvailable,
            LimitSignals = Array.AsReadOnly(signals),
            Sense = new MotionAxisSenseState
            {
                MotionSenseKind = identity.MotionSenseKind,
                MotionSenseBasis = identity.MotionSenseBasis,
                SenseChannelStatus = MotionVocabulary.StatusNotAvailable,
                PositionValueStatus = MotionVocabulary.PositionValueNotRead,
                PositionDisplayValue = null,
                PositionUnit = MotionVocabulary.SpeedUnitControllerUnits,
            },
            Safety = new MotionAxisSafetyState
            {
                // Motion-local protections only. They are NOT the Cleaning-process interlocks and are
                // never merged with them: an unverified protection here says nothing about a process
                // interlock, and a process interlock never appears in this block.
                EmergencyStopVisibilityStatus = MotionVocabulary.StatusNotAvailable,
                StopCircuitVerificationStatus = MotionVocabulary.VerificationNotVerified,
                ControlledStopCapabilityStatus = MotionVocabulary.StatusUnresolved,
                ControlledStopVerificationStatus = MotionVocabulary.VerificationNotVerified,
                IsolationVerificationStatus = MotionVocabulary.VerificationNotVerified,
                LocalProtectionStatus = MotionVocabulary.VerificationNotVerified,
                ResetEncoderVerificationStatus = MotionVocabulary.VerificationNotVerified,
            },
            Kinematic = new MotionAxisKinematicState
            {
                KinematicConversionStatus = MotionVocabulary.StatusUnresolved,
                KinematicConversionIsPerAxis = true,
                PublishedUnit = MotionVocabulary.SpeedUnitControllerUnits,
                ConversionProvenanceRequirement = MotionVocabulary.ProvenanceMeasuredDuringCommissioning,
            },
            MoveAuthorization = new MotionAxisMoveAuthorization
            {
                MoveAuthorizationState = MotionVocabulary.CommandStateRefused,
                MoveRefusalReasons = MotionVocabulary.AllRefusalReasons,
            },
        };
    }
}
