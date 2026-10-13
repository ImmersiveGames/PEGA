# Face System Architecture V2

## Purpose

The Face System must evolve from a fixed Eyes/Mouth architecture into a fully dynamic track-based atlas facial animation framework.

The system should support any number of user-defined facial tracks without requiring code changes.

Examples:

* eyes
* mouth
* eyebrows
* cheeks
* tears
* sweat
* accessories
* fx

The runtime must treat all tracks equally.

No special-case logic should exist for Eyes or Mouth.

---

# Core Principles

The system must remain:

* Data-driven
* Atlas-based
* Lightweight
* ScriptableObject-based
* Animator-free
* Predictable
* Extensible
* MaterialPropertyBlock-based

---

# High Level Architecture

```text
FaceSpriteLibrary
        ↓
FaceState
        ↓
FaceAnimation
        ↓
FaceController
        ↓
Track Bindings
        ↓
Renderer + Material Index
```

---

# Dynamic Tracks

Tracks are identified by a unique Track ID.

Examples:

```text
eyes
mouth
eyebrows
fx
```

Tracks must be completely user-defined.

The runtime must never assume any specific track exists.

---

# FaceSpriteLibrary

The library becomes track-based.

Each track contains:

* Track ID
* Atlas Texture
* Grid Size
* Sprite Entries

Sprite Entry:

* Sprite ID
* Atlas Coordinate

Example:

Track: eyes

* eye.normal.open
* eye.normal.half
* eye.normal.closed

Track: eyebrows

* eyebrow.normal
* eyebrow.angry
* eyebrow.sad

Runtime lookups must be deterministic and dictionary-backed.

---

# FaceState

FaceState represents the persistent facial appearance.

A FaceState contains Track States.

Track State:

* Track ID
* Default Frame ID
* Frame Mappings

Frame Mapping:

* Semantic Frame ID
* Sprite ID

Example:

Track: eyes

* opened -> eye.normal.open
* half -> eye.normal.half
* closed -> eye.normal.closed

Track: eyebrows

* neutral -> eyebrow.normal
* angry -> eyebrow.angry

States do not animate.

The Default Frame ID explicitly selects the persistent frame for the track.
It must reference one of the Track State's semantic Frame Mappings. Frame list
ordering does not affect runtime behavior.

---

# FaceAnimation

FaceAnimation represents temporary facial behavior.

FaceAnimation contains Animation Tracks.

Animation Track:

* Track ID
* Enabled
* Frame Source
* Frames

Frame Source:

* FromState
* CustomLibrary

FromState:

Uses semantic frame IDs resolved through the active FaceState.

CustomLibrary:

Uses sprite IDs resolved directly through FaceSpriteLibrary.

Animations only override tracks they contain.

---

# Track Bindings

Track Bindings connect a Track ID to a render target.

Binding contains:

* Track ID
* Renderer
* Material Index

Example:

eyes
→ HeadRenderer
→ Material Index 1

mouth
→ HeadRenderer
→ Material Index 2

eyebrows
→ HeadRenderer
→ Material Index 3

Track bindings must support:

* MeshRenderer
* SkinnedMeshRenderer

---

# Material Index Support

Material Index support is mandatory.

The system must apply MaterialPropertyBlocks using:

```csharp
renderer.SetPropertyBlock(block, materialIndex);
```

The system must support:

* Separate renderers
* Shared renderers
* Multiple facial tracks on the same renderer

---

# FaceController

FaceController becomes fully track-based.

Responsibilities:

* Active FaceState
* Registered FaceAnimations
* Track Bindings
* Runtime track overrides

Runtime order:

1. Apply FaceState
2. Apply active animation overrides
3. Update render bindings

Animations override only their own tracks.

When animations stop, tracks naturally return to the active FaceState.

No restore stack.

No state graph.

No blend tree.

No animator dependency.

---

# Validation

Validate:

* Empty Track ID
* Duplicate Track IDs
* Missing atlas
* Missing sprite ID
* Missing semantic frame ID
* Invalid coordinates
* Invalid grid size
* Missing renderer
* Invalid material index

Never silently fallback.

Never guess missing data.

---

# Rendering Examples

## Separate Meshes

```text
Character
├── EyesQuad
└── MouthQuad
```

Bindings:

eyes → EyesQuad → index 0

mouth → MouthQuad → index 0

---

## Shared Renderer

```text
Character
└── HeadMesh
    └── SkinnedMeshRenderer
```

Materials:

0 Skin

1 Eyes

2 Mouth

3 Eyebrows

Bindings:

eyes → index 1

mouth → index 2

eyebrows → index 3

---

# Design Goal

The Face System should become a generic dynamic track-based facial atlas animation framework.

Users should be able to create new facial tracks entirely through assets and inspectors without requiring code changes.
