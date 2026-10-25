using System;
using System.Collections.Generic;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Travel to Escape Target")]
    public sealed class PEGANpcTravelToEscapeAction : PEGANpcNavigationAction
    {
        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachSelectedEscapeDestination;

        protected override PEGANpcDesireGoalKind ActionGoalKind =>
            PEGANpcDesireGoalKind.ReachKnownEscapeDestination;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.TravelToEscapeTarget,
            ActionEffectKind,
            ActionGoalKind,
            PEGANpcPlanningTargetKind.EscapeDestination,
            true,
            "NPC began escaping to a known exit.");

        public bool TryCreatePlanningCandidate(
            PEGANpcPlanningTarget knownEscapeTarget,
            PEGAActorMovementSpeedRegime speedRegime,
            out PEGANpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            candidate = default;
            if (knownEscapeTarget.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                knownEscapeTarget.Kind != PEGANpcPlanningTargetKind.EscapeDestination ||
                !knownEscapeTarget.TargetId.IsValid)
            {
                diagnostic = "Escape planning requires a valid escape Target already resolved from this NPC's Knowledge State.";
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    knownEscapeTarget.KnownPosition,
                    speedRegime,
                    out PEGANpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            candidate = new PEGANpcPlanningActionCandidate(
                Contract,
                this,
                knownEscapeTarget.TargetId,
                true,
                speedRegime,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            List<PEGANpcPlanningTarget> targetList = new List<PEGANpcPlanningTarget>();
            List<PEGANpcPlanningActionCandidate> candidateList = new List<PEGANpcPlanningActionCandidate>();
            for (int index = 0; index < context.KnownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = context.KnownTargets[index];
                if (target.Kind != Contract.TargetKind ||
                    target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState)
                {
                    continue;
                }

                if (TryCreatePlanningCandidate(
                        target,
                        PEGAActorMovementSpeedRegime.Pursuit,
                        out PEGANpcPlanningActionCandidate candidate,
                        out string routeDiagnostic))
                {
                    targetList.Add(target);
                    candidateList.Add(candidate);
                }
                else if (string.IsNullOrWhiteSpace(diagnostic) && !string.IsNullOrWhiteSpace(routeDiagnostic))
                {
                    diagnostic = routeDiagnostic;
                }
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            if (candidateList.Count > 0)
            {
                diagnostic = string.Empty;
                return true;
            }

            return string.IsNullOrWhiteSpace(diagnostic);
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPursuitContinuation? continuation) =>
            CandidateMatchesContract(candidate) && !continuation.HasValue;

        public override bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPursuitContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (continuation.HasValue || plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.KnowledgeState)
            {
                diagnostic = "Escape plans are only valid as Normal Desire goals backed by Gang Knowledge State.";
                return false;
            }

            PEGANpcGangMembership membership = GetComponent<PEGANpcGangMembership>();
            if (membership == null || !membership.TryGetKnownEscapeTargets(
                    out IReadOnlyList<PEGANpcPlanningTarget> targets, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "This Actor has no current Gang escape targets."
                    : diagnostic;
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                PEGANpcPlanningTarget target = targets[index];
                if (target.TargetId == plan.TargetId && target.IsValid && target.HasEvidenceTime &&
                    target.Kind == Contract.TargetKind && target.Provenance == PEGANpcPlanningTargetProvenance.KnowledgeState &&
                    target.KnownPosition.Equals(plan.TargetPosition) &&
                    Math.Abs(target.EvidenceTimeSeconds - plan.EvidenceTimeSeconds) <= 0.000001d)
                {
                    currentKnownPosition = target.KnownPosition;
                    evidenceTimeSeconds = target.EvidenceTimeSeconds;
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "The selected escape destination is no longer present in current Gang Knowledge State.";
            return false;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPursuitContinuation? continuation,
            out string diagnostic)
        {
            if (!IsCurrentPlanValid(plan, continuation, out _, out _, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            return TryStart(plan.TargetPosition, plan.SpeedRegime, out diagnostic);
        }

        public bool TryStart(
            PEGANpcEscapeDestination selectedEscapeDestination,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            if (selectedEscapeDestination == null)
            {
                diagnostic = "A selected PEGA escape destination is required by this Action.";
                RejectStart(diagnostic);
                return false;
            }

            return TryStart(selectedEscapeDestination.WorldPosition, speedRegime, out diagnostic);
        }

        public bool TryStart(
            Vector3 selectedEscapeTarget,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            return TryStartNavigation(selectedEscapeTarget, speedRegime, out diagnostic);
        }
    }
}
