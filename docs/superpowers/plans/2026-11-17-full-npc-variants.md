# Full NPC Behavior Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure the three complete NPCs so their GOAP autonomy initializes and can choose POI travel, opportunistic item collection, pursuit or avoidance, and attack.

**Architecture:** Reuse the existing base Actor, ten unique Action providers, Group POI knowledge, and causal planner. Extend the PEGA Attack Action so GOAP can select an approach followed by an attack, create test-specific Desire Settings and three derived Desire Profiles, and update only the three complete NPC instances in the mission scene. Do not add parallel runtime architecture or change Immersive packages.

**Tech Stack:** Unity YAML scene and ScriptableObject assets; existing PEGA NPC Action, Desire, Group, and knowledge contracts.

**Spec:** `docs/superpowers/specs/2026-11-17-full-npc-variants-design.md`

## Global Constraints

- Do not change stable Action IDs, physical attack/impact behavior, or Immersive packages.
- Calibrate only new Desire Settings used by the three complete test profiles; preserve existing specialist Settings and treat test weights as provisional.
- Do not change logger entries, categories, levels, filters, messages, or instrumentation.
- Do not alter capture, impact, incapacitation, knockback, or item-drop mechanics.
- Preserve the existing specialized NPC configurations and all unrelated workspace changes.
- Do not run Unity import/build/tests/Play Mode/smoke/batchmode.
- Do not mark #93 complete based on serialized composition or static checks; runtime behavior still needs user validation in Unity.

## Review Focus

- Duplicate stable Action ID: each NPC must have one provider for each of these IDs: `approach-known-target`, `attack-known-target`, `investigate-last-known-target-position`, `maintain-safe-separation-from-known-character`, `investigate-perceived-area-presence`, `investigate-unresolved-radial-presence`, `navigate-to-known-group-object-entry-position`, `navigate-to-known-individual-object-entry-position`, `investigate-known-group-poi`, and `pick-up-known-object-entry`.
- Duplicate Desire ID: each derived profile must contain no repeated `PegaNpcDesireDefinition.DesireId` across its context.
- Group knowledge access: each complete actor must keep `Gang` and also join `SecondGroup` so the existing shared POI facts are available.
- Prefab overrides: retain the existing scene-specific query binding, group knowledge reader, capture proof policy, Actor identity, and global knowledge references.
- Workspace preservation: the scene and other project files have pre-existing user changes; make only the approved overrides and retain unrelated content.

---

### Task 1: Create three derived complete behavior profiles

**Files:**
- Create: `Assets/PEGA/NPC/Desires/Profiles/Demo_CompleteConfrontation.asset` and `.meta`
- Create: `Assets/PEGA/NPC/Desires/Profiles/Demo_CompleteSelfPreservation.asset` and `.meta`
- Create: `Assets/PEGA/NPC/Desires/Profiles/Demo_CompleteOpportunistic.asset` and `.meta`
- Create: profile-specific Attack Desire Settings with weights 48 (Confrontation), 24 (Self-Preservation), and 32 (Opportunistic)
- Create: a profile-specific Self-Preservation Separation Setting with weight 48
- Read-only references: the three existing source profiles and their DesireSettings under `Assets/PEGA/NPC/Desires/`

**Interfaces:**
- Reuse `PegaNpcDesireProfile` script GUID `db47bbb1ad3143a419812a7fe2741c7b`.
- Preserve the source profile's `Normal` context and plan-selection policy (`10` seconds reference, existing `PegaNpcPlanScorePolicy` GUID `c0239dde8a634094a59df2bd7b32d10b`).
- `Demo_CompleteConfrontation` keeps its source behavior and adds its Attack setting (weight 48), `Demo_OpportunisticPickUpObjectEntry` (weight 16), and `Demo_OpportunisticSecondGroupPoiInvestigation` (weight 24).
- `Demo_CompleteSelfPreservation` uses its profile-specific Separation setting (weight 48), retains low-weight pursuit and investigation settings, and adds its Attack setting (weight 24), Pick Up (weight 16), and POI Investigation (weight 24).
- `Demo_CompleteOpportunistic` keeps its source behavior and adds its Attack setting (weight 32), `Demo_SelfPreservationFocus_RickPursuit` (weight 0.5), and `Demo_PursuitFocus_Separation` (weight 0.5).

- [x] Create each profile by retaining its source behavior, using the approved local test Settings, and appending the required Pick Up/POI/pursuit/avoidance Settings.
- [x] Keep each source profile's plan comparison and `Normal` context unchanged.
- [x] Statically verify each referenced setting resolves, its approved test weight is serialized, and it contributes a unique Desire ID within that profile.

### Task 2: Wire the complete profiles and unique Action providers into the mission scene

**Files:**
- Modify: `Assets/PEGA/Scenes/MissionActivityContent.unity`
- Read-only references: `Assets/PEGA/NPC/Prefabs/PEGA_NPCActorRuntimeBase.prefab`, existing Action Definition assets, `PegaNpcPlanningActionId`, Group profile assets, and scene knowledge objects

**Interfaces:**
- Complete Confrontation instance: PrefabInstance anchor `880000000000000001`; set its `desireProfile` to `Demo_CompleteConfrontation`.
- Complete Self-Preservation instance: PrefabInstance anchor `880000000000000002`; set its `desireProfile` to `Demo_CompleteSelfPreservation`.
- Complete Opportunistic instance: PrefabInstance anchor `880000000000000003`; set its `desireProfile` to `Demo_CompleteOpportunistic`.
- Set each `availableBehaviors.Array.size` to `10`; retain the existing Group Pick Up definition and remove the Individual Pick Up definition from these three arrays.
- The Group provider is `Assets/PEGA/NPC/Actions/Definitions/PickUpKnownObjectEntry.asset` (GUID `e4dbed8869b74e549745506b7703ab51`); the excluded Individual provider is `PickUpKnownIndividualObjectEntry.asset` (GUID `f82ad38b598042d39628650820397efc`).
- Set each `groupProfiles.Array.size` to `2`, with Gang profile GUID `687bff7f733a4fc29ac00e12b44aef60` at index 0 and SecondGroup profile GUID `3779cf2dab06404e8cd914a9262bc383` at index 1.
- Preserve existing query binding `442300475`, Group knowledge reader, capture proof policy, actor hierarchy/identity, and per-instance `globalKnowledgeState` references.

- [x] Replace only the three complete NPC `desireProfile` overrides with the new derived profile assets.
- [x] Reduce each complete NPC Action array from 11 to 10 entries by removing the Individual Pick Up provider; leave the Group Pick Up provider and all other existing Actions intact.
- [x] Configure both existing Group memberships for each complete NPC, keeping Gang at index 0 and adding SecondGroup at index 1.
- [x] Statically verify every profile/action/group GUID resolves; each NPC has exactly the ten unique stable Action IDs listed under Review Focus; the Group provider is present and the Individual provider is absent.
- [x] Verify unrelated scene content and existing specialist NPCs were not edited; run `git diff --check`.

---

### Task 3: Plan an approach followed by an attack

**Files:**
- Modify: `Assets/PEGA/NPC/Actions/Runtime/PegaNpcAttackKnownCharacterAction.cs`
- Modify: `Assets/PEGA/Movement/NPC/PEGANpcNavMeshMovementIntentSource.cs`
- Modify: `Assets/PEGA/Scenes/MissionActivityContent.unity` (test-instance budget overrides only)
- Read-only contracts: `PEGANpcActionProviderAsset`, `PEGANpcCausalPlanEvaluation`, `PegaNpcTargetReachedEffect`, and `PegaActorAttackMechanic`

**Behavior:**
- Offer Attack Known Character for the current perception-backed target outside attack range.
- Allow direct Attack only when its current physical region is satisfied.
- Allow projected Attack only after `ApproachKnownCharacter` reaches the same Actor Target with matching evidence.
- Keep the three complete test NPCs at the existing 128 evaluation budget after the 1024 experiment caused repeated budget exhaustion and severe frame-rate loss; optimize the planner before raising the limit again.
- Map a projected `PathInvalid` query to `Invalid/Unavailable` so an invalid route does not become an incomplete-step diagnostic.
- Preserve current occurrence, perception evidence, front/range checks, and the existing physical attack path at execution and impact.

- [x] Add state-aware evaluation for direct and projected Attack steps.
- [x] Remove the premature region rejection from candidate and causal definition creation while retaining physical checks.
- [x] Restore the three complete test NPC budgets to 128 and classify projected invalid paths as unavailable.
- [x] Keep Player/NPC physical execution on the existing shared Attack mechanic.

## Manual Unity Validation (Pending)

- Open `MissionActivityContent` and confirm the three complete NPCs reference their derived Desire Profiles, ten Action Definitions, and both Group profiles.
- Enter Play Mode and confirm each complete NPC logs successful GOAP initialization without the duplicate `pick-up-known-object-entry` diagnostic.
- Observe each NPC navigating nearby SecondGroup POIs, collecting a known item opportunistically, reacting to the Player by pursuit or avoidance according to its focus, and selecting/executing attack when the attack conditions apply.
- Confirm the existing attack logger output identifies NPC attempt and result and that Player attacks still work.
- Capture the logs and behavior evidence for review. Keep #93 open until behavior is demonstrated; do not run Play Mode as part of this implementation session.
