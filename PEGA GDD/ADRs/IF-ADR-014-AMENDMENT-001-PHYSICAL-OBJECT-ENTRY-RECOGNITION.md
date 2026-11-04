# IF-ADR-014 Amendment 001 — Physical Recognition of Object Entry Occurrences

Status: **Proposed for acceptance** (architecture direction agreed; API review and implementation pending)  
Proposed: **2026-11-03**  
Amends: [IF-ADR-014 — Authored Definition and Stable Identity Authority](https://github.com/ImmersiveGames/com.immersive.framework/blob/master/Documentation~/Architecture/ADRs/IF-ADR-014-Authored-Definition-and-Stable-Identity-Authority.md), specifically the Stable Object Binding and public read-only Object Entry occurrence query boundaries  
Related: IF-ADR-001 (lifecycle authority); IF-ADR-049 and IF-ADR-050 (Actor occurrence, precedent only); [IF-GOV-001](https://github.com/ImmersiveGames/com.immersive.framework/blob/master/Documentation~/Architecture/Governance/IF-GOV-001-API-MATURITY-AND-VALIDATION-GOVERNANCE.md)  
Consumer motivation: PEGA Trello [#37 — Target selection and contextual interaction](https://trello.com/c/wWbleLBj)

> **Decision boundary:** This amendment records the intended architecture and invariants. It does **not** approve an exact public API signature, implement any code, certify Unity behavior, or promote Experimental contracts to Stable. Its acceptance and implementation follow the Framework's existing governance. This document must not be interpreted as authorization to implement object profiles, object composition, or gameplay interactions within the scope below.

## 1. Context and problem

The Framework already provides `ObjectEntryId`, `ObjectEntryDeclaration`, owner-aware committed `StableObjectBinding` records, and the read-only `IObjectEntryOccurrenceQuery.Resolve(StableObjectReference)` operation. These resolve a *known logical identity* to its current physical `GameObject` when the applicable binding is available and unambiguous.

A spatial perception consumer has the inverse starting point: it detects a Unity `Collider`, but does not necessarily know the Object Entry identity. Forcing the consumer to interpret Framework declarations and binding validity would duplicate the Framework's physical-occurrence authority. A declaration found in a hierarchy is not, by itself, proof of an admitted occurrence.

The requirement is to recognize a physical candidate under the existing owner and lifecycle authority, without introducing a world-object manager, spatial index, collider registry, new identity model, or gameplay-specific semantics. Multiple colliders may belong to one logical object; ordinary scene geometry may have no Object Entry declaration.

## 2. Decision

### 2.1 Complementary, optional physical-recognition capability

The Framework will provide an **optional, read-only physical-recognition consumer capability** alongside, not in place of, `IObjectEntryOccurrenceQuery`.

- It receives physical evidence obtained by the caller (conceptually a Unity `Collider`), discovers the nearest applicable `ObjectEntryDeclaration` in the collider's ancestor chain, and checks the corresponding committed physical binding.
- The capability is implemented over the **existing** `ObjectEntryOccurrenceQueryBinding`, `StableObjectBindingRegistry`, host and Runtime Content transactions. It does not introduce another anchor, lookup authority, registry, publication channel or lifecycle.
- `IObjectEntryOccurrenceQuery.Resolve(StableObjectReference)` and its existing result, selector, ambiguity and lifetime semantics remain unchanged.
- The public interface name, operation name (e.g., `Recognize`), result type and exact signatures are **deferred to the Framework API review**. Preserving compatibility with external implementations of the existing interface is a reason to prefer a distinct optional contract rather than an overload on that interface.

### 2.2 Recognition semantics

For a supplied physical candidate, recognition proceeds as follows:

1. Inspect the candidate and its Unity ancestor chain. The **nearest** `ObjectEntryDeclaration` is the identity boundary. `Transform.root`, a shared `Rigidbody`, Unity name, tag or hierarchy path is not the functional object identity.
2. When no declaration exists in that chain, return **Unrecognized** as a normal outcome; ordinary scene objects remain valid without Framework identity.
3. When the nearest declaration is invalid, return an explicit **Invalid** outcome. Do **not** skip it and silently attribute the collider to an outer declaration.
4. For a valid declared `ObjectEntryId`, require a **live, committed** binding whose ID and bound physical `GameObject` match the **exact GameObject holding that declaration**. A declaration in a loaded but uncommitted scene does not qualify.
5. One matching committed binding under **one exact `RuntimeContentOwner`** may yield **Resolved**. Zero eligible bindings yield **Unavailable**; multiple eligible authorities or duplicate active binding records yield **Ambiguous**, not a first-match result.
6. Destroyed or released physical occurrences must not be returned as resolved. Recognition is a point-in-time read; rollback, release and host teardown remain the existing authorities for invalidating access and removing bindings.
7. Descendant colliders with the same nearest declaration identify the same logical object and physical root. No per-collider registry or persistent collider-to-object mapping is introduced.

The concrete status vocabulary and diagnostic payload will be finalized in API review, preserving these distinctions.

### 2.3 Exact-root and owner ambiguity rules

`Resolved` means that exactly **one committed binding record under one exact owner authority** supports the ID and declaration GameObject found from physical evidence.

- An identical `ObjectEntryId` on a **different root** does not make recognition of the supplied collider ambiguous merely because the ID is shared. The physical candidate identifies the exact root. Identity-based `Resolve(StableObjectReference)` still follows its existing selector and ambiguity rules.
- The **same ID and root under two owners** is `Ambiguous`.
- **Multiple active records for the same `(ObjectEntryId, RuntimeContentOwner)` key** remain ambiguity evidence, even if one physical candidate appears to match. Recognition must not silently discard the duplicate binding evidence or choose by enumeration order.
- The query anchor's owner controls the **lifetime of access to the query capability**, not which target owner may be recognized. Releasing the anchor invalidates the capability; releasing the target owner invalidates its target binding.
- Exact owner equality includes scope, stable owner identity and definition-instance token. It is not inferred from scene names, the collider hierarchy, the current Activity or the anchor owner.

These rules preserve the accepted Object Entry binding authority in IF-ADR-014; the physical query never changes or repairs that registry as a side effect.

### 2.4 Public result and authority boundary

A successful recognition result must expose sufficient **point-in-time evidence** for its consumer: status, recognized `ObjectEntryId`, physical occurrence root (`GameObject`) and appropriate diagnostics. No separate persistent Object Entry occurrence ID is introduced in this amendment.

**`RuntimeContentOwner` does not need to be public in this result.** The Framework retains and verifies the exact owner internally. The consumer needs the recognized target, not lifecycle authority; returning the owner would not make a stale result durable. If a future consumer demonstrates a legitimate need for owner-scoped operations, that is a separate contract review.

The result does not grant gameplay permission, persistent location knowledge, target eligibility or access to perform actions. It cannot itself publish Knowledge. Consumers must handle results as momentary evidence and recheck when their operation depends on current availability.

### 2.5 Existing and future object composition

`ObjectEntryDeclaration` remains a **lightweight logical identity declaration**. Simple Unity scene objects may have no declaration; identified objects may have a declaration without any profile or composition.

Future object profiles, optional part composition and controlled physical presentation may be evaluated separately using `com.immersive.composition` through Framework adapters. They must not redefine `ObjectEntryId`, turn non-Actor objects into Actors, or make composition mandatory for identified objects. As long as composed colliders remain descendants of the declaration's root, they follow the same ancestor-recognition rule. A part with its own declaration is an independent identity boundary and requires its own admitted binding.

This amendment does **not** implement new Runtime Content admission for dynamically spawned, transferred or independently materialized Object Entries. Those cases require separate lifecycle decisions. Existing committed Route/Activity content is the initial coverage.

## 3. Alternatives considered

| Alternative | Disposition |
|---|---|
| Add `Resolve(Collider)` to `IObjectEntryOccurrenceQuery` | Not the preferred direction: it changes the existing interface contract for external implementers and mixes known-ID resolution with physical recognition. Exact API choice remains subject to review. |
| Make each PEGA consumer find the declaration and resolve by ID | Rejected as the canonical architecture: duplicates the Framework's identity/binding validation and risks mistaking a declaration for a committed occurrence. |
| Maintain a separate collider lookup table, global object manager or parallel lifecycle | Rejected: the existing hierarchy and committed binding registry suffice for the defined case. |
| Require `Rigidbody`, `Transform.root`, Actor identity or object profiles | Rejected: these are not universal logical-identity boundaries. |
| Place perception geometry or gameplay interactions inside Framework Object Entry | Rejected: Perception discovers physical candidates; the game owns interaction eligibility and behavior. |

## 4. Compatibility and implementation boundaries

- Maintain the current `StableObjectReference` resolution and the existing Object Entry authoring model without migration.
- Add the optional public contract and result only after API review; reuse the existing owner-bound query component and committed binding table.
- Do not modify the established Route/Activity prepare, commit, rollback and release authority unless source evidence demonstrates a necessary correction to meet the invariant above.
- Do not add general object profiles, composers, dynamic object spawners, Object Entry Session admission or a spatial-search API as part of this change.
- Public additions remain **Experimental** and must follow IF-GOV-001.
- Update IF-ADR-014 cross-references, the Public API and Object Entry occurrence guide, and changelog when implementation actually lands. The ADR must distinguish normative decisions from implemented/validated behavior.

## 5. Required conformance evidence

Review and verification of the eventual implementation should establish these cases without expanding into unrelated gameplay or framework revalidation:

1. A collider with no ancestor declaration returns `Unrecognized` without registering an object.
2. The nearest declaration is used; an invalid nearest declaration does not fall back to a higher one.
3. A valid declaration before commit or after rollback/release is not `Resolved`; a committed live binding can be recognized.
4. Several colliders beneath the same declaration yield the same `ObjectEntryId` and physical root.
5. The same ID on separate roots is distinguishable by physical evidence; existing identity-based resolution keeps its previous ambiguity semantics.
6. Duplicate active bindings under one composite key and one root admitted by multiple owners are reported `Ambiguous` rather than selected arbitrarily.
7. An anchor may recognize another owner's target, while anchor release invalidates query access and target release invalidates that target's occurrence.
8. A destroyed, replaced or uncommitted physical root is not returned as a current resolved occurrence.
9. `IObjectEntryOccurrenceQuery.Resolve(StableObjectReference)` and current consumers remain compatible.
10. Recognition does not mutate the binding registry, publish Knowledge, or decide interaction eligibility.

## 6. Deferred decisions and limits

The following remain explicitly **open** for separate work: final public names and signatures; any need to expose owner data publicly; specialized physical association outside the declared hierarchy; optional Object Entry profiles and composition; dynamic object admission and owner transfers; any identity/lifecycle model for mobile items; 3D spatial discovery and geometry in PEGA Perception; contextual interaction and feedback in PEGA #37.

The accepted direction for future advanced objects is **optional extensibility**, not an approved object-profile implementation. The Framework remains the authority for identity and physical occurrence only where its identity contracts apply; Actor identity and lifecycle remain separate domains.

## 7. Acceptance and current status

This amendment is a **proposed normative architectural record**, derived from the reviewed Framework/PEGA investigation and the approved direction for recognition. The detailed API and implementation have **not** been approved by this document. No source changes, tests, Unity run, version promotion or release are claimed.

Acceptance should be recorded through the Framework's existing ADR practice. Once accepted, any implementation plan or change that conflicts with §§2–4 must explicitly revise this amendment rather than silently narrowing the intended behavior to what is easiest to implement.
