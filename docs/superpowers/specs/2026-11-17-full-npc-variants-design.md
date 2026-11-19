# Full NPC Behavior Variants Design

## Intent

Make the three complete NPCs in `MissionActivityContent` capable of running the same gameplay loop: travel among nearby POIs, collect known items when an opportunity appears, pursue or avoid the Player according to their existing behavior priorities, and attack when their existing GOAP desires select that action. This prepares a meaningful #93 validation scene; it does not certify the gameplay behavior.

## Evidence and diagnosis

The latest Play Mode log reports, for each of the three complete NPCs:

> `NPC Attack GOAP runtime initialization failed ... configuredActionCount='11' attackActionConfigured='True' ... More than one composed Action definition uses stable Action ID 'pick-up-known-object-entry'.`

`PEGANpcAutonomy` rejects the composed Action set when stable Action IDs are duplicated. The Group and Individual `Pick Up Known Object Entry` definitions share that ID because they are two knowledge-source configurations of the same planning action. As a result, the GOAP autonomy does not initialize; there are no subsequent selection/execution events for these actors. The Player's attack execution and impact logs in the same capture show that this failure is on the NPC planning/composition path.

The approved content choice is **A: Group POIs for these three complete NPCs**. Each complete NPC will compose the Group provider only. The existing Individual provider and its specialist setup remain available and unchanged.

## Chosen design

Update the three complete NPC scene compositions, add three derived Desire Profile assets, and extend the PEGA Attack Action's causal planning so GOAP can select an approach followed by attack. Do not add parallel runtime architecture or change Immersive packages.

### Shared Action composition

- Compose the 10 Action Definition assets that represent 10 unique stable Action IDs.
- Keep the existing attack, approach/movement, perception/investigation, object-navigation, interaction, and intermediate Actions.
- Include `PickUpKnownObjectEntry.asset` (the Group knowledge provider); omit `PickUpKnownIndividualObjectEntry.asset` from these three compositions to avoid duplicate stable IDs.
- Preserve the base Actor mechanics, Object Entry query binding, Group knowledge reader, capture proof policy, and existing scene knowledge references.

### Behavior priorities

Each new Desire Profile starts from the corresponding existing profile. It may use profile-specific test Desire Settings for Attack and Separation; these weights are provisional calibration for this test, not final balance. Preserve existing specialist Settings and Desire Definitions.

| Complete NPC | Keep its current focus | Add existing settings | Resulting capability priorities |
| --- | --- | --- | --- |
| Confrontation | `Demo_PursuitFocus` | Attack weight 48; Opportunistic Pick Up; SecondGroup POI Investigation | Strongly favors pursuing and attacking a visible Player; continues POI travel and opportunistic collection when no attack plan applies |
| Self-Preservation | `Demo_SelfPreservationFocus` | Separation weight 48; Attack weight 24; Opportunistic Pick Up; SecondGroup POI Investigation | Strongly favors creating distance from a nearby Player while retaining a lower-priority attack plan and its POI/item behavior |
| Opportunistic | `Demo_SecondGroupOpportunisticPickUp` | Attack weight 32; low-weight pursuit and low-weight separation from the existing Self-Preservation/Pursuit settings | Keeps POI/item behavior active while treating a nearby attack as a meaningful opportunity |

These calibrated Settings belong only to the three complete test profiles. The V01 Attack, existing specialist profiles, and their Settings remain unchanged.

### Attack planning flow

- Offer Attack Known Character for a current perception-backed Actor target even when it is outside the physical attack region.
- Let causal planning compose `ApproachKnownCharacter → AttackKnownCharacter` only when projected arrival matches the same Actor Target and evidence.
- Keep the existing per-NPC causal search budget at 128 for the three complete test actors after the 1024 experiment produced repeated exhaustion and severe frame-rate loss; reconsider only after planner optimization.
- Classify a projected `NavMeshPathStatus.PathInvalid` query as `Invalid/Unavailable`, not `Unknown`, matching the route query from the live Actor position.
- Keep current evidence, official occurrence, physical front/range checks, and the attack mechanic authoritative at execution and impact. Planning an impact does not count as a hit.

For the three complete actors, configure both existing Group memberships: preserve inherited/current `Gang` membership and add `SecondGroup`, whose shared knowledge contains the POI facts. Retain the existing group reader and each actor's current scene knowledge configuration.

### Observable outcome

No logging changes are part of this design. The existing enabled `PEGA.Gameplay.Attack.Events` Debug rule already covers its `.GOAP` and `.Player` subnamespaces. Once initialization succeeds, the expected attack evidence is the existing GOAP initialization/selection/execution sequence and existing attack attempt/result events for both NPCs and Player. The evidence must also show the NPCs' POI/item behavior during manual Play Mode validation.

## Alternatives considered

- **A — Group provider for the three complete NPCs (chosen):** supports the approved nearby Group POI circuit, avoids composing two definitions with one stable ID, and leaves the Individual specialist path intact.
- **B — Expand the shared provider to accept both knowledge scopes:** changes a shared Action contract/runtime path and is unnecessary for the approved Group-only POI circuit. This design does not authorize that generalization.

## Constraints and non-goals

- Do not change stable Action IDs, physical attack/impact behavior, or Immersive packages.
- Calibrate only the new Settings referenced by the three complete test profiles; preserve specialist Settings and treat these values as provisional.
- Do not change logger entries, categories, levels, filters, messages, or instrumentation.
- Do not alter capture, impact, incapacitation, knockback, or item-drop mechanics.
- Preserve the existing specialized NPC configurations and all unrelated workspace changes.
- Do not mark #93 complete based on serialized composition or static checks; runtime behavior still needs user validation in Unity.

## Acceptance

- Each of the three complete NPCs has one Action provider per stable Action ID (10 unique IDs), including the Group Pick Up provider and the existing Attack and intermediate Actions.
- A visible target outside attack range produces a causal plan containing Approach then Attack; direct attack remains available when the current physical attack region is satisfied.
- Each NPC has its own derived Desire Profile retaining its role behavior and using only profile-specific provisional Attack/Separation calibration; all Desire IDs are unique per profile.
- All three retain `Gang` and also belong to `SecondGroup`; required Group knowledge, POI facts, query, reader, actor, and capture-proof references remain intact.
- Static checks find no duplicate stable Action IDs in any of the three compositions, no duplicate DesireDefinition IDs in the new profiles, and no missing asset references.
- Logger configuration and runtime instrumentation are unchanged.
- Unity import and Play Mode validation remains pending. Manually confirm GOAP initialization and decisions, POI travel, opportunistic Pick Up, pursuit/avoidance, NPC attacks, Player attacks, and attack results before accepting #93.
