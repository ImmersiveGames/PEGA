# Face System Specification

## Architecture

The Face System is a data-driven, atlas-based facial animation framework.
Runtime behavior is organized entirely by user-defined Track IDs.

```text
FaceSpriteLibrary
        |
FaceState
        |
FaceAnimation
        |
FaceController
        |
Track Binding
        |
Renderer + Material Index
```

The runtime must not assume that tracks such as `eyes` or `mouth` exist.
Projects can define any tracks required by their characters, including
`eyebrows`, `tears`, `accessories`, or `fx`.

## FaceSpriteLibrary

`FaceSpriteLibrary` contains a list of sprite tracks.

Each sprite track contains:

- Unique Track ID
- Atlas texture
- Grid size
- Sprite entries

Each sprite entry maps a Sprite ID to an atlas coordinate. Runtime lookups are
dictionary-backed and deterministic.

## FaceState

`FaceState` represents the persistent facial appearance and contains a list of
Track States.

Each Track State contains:

- Track ID
- Default Frame ID
- Semantic frame mappings

Each mapping connects a semantic Frame ID to a Sprite ID in the matching
library track.

The Default Frame ID explicitly identifies the persistent frame. It resolves
through the semantic mappings and is independent from list ordering. All
mappings remain available to `FromState` animations.

States do not perform timing or playback.

## FaceAnimation

`FaceAnimation` contains a dynamic list of Animation Tracks.

Each Animation Track contains:

- Track ID
- Enabled state
- Frame source
- Timed frames

`FromState` frames resolve semantic Frame IDs through the active `FaceState`.
`CustomLibrary` frames resolve Sprite IDs through the animation's
`FaceSpriteLibrary`.

Animations override only the Track IDs they contain. When an animation ends,
the next update naturally restores the active state's base frame.

Playback modes:

- Sequential
- Loop
- Interval
- Random

## FaceController

`FaceController` owns:

- Registered states
- Registered animations
- Active state
- Active animation players
- Track bindings

Runtime order:

1. Resolve each state's base track frame.
2. Resolve active animation overrides.
3. Apply the final frame for each Track ID to its binding.

Starting an animation stops existing animation playback only for matching
Track IDs. There is no restore stack, animator graph, blend tree, or state
graph.

## Track Bindings

A Track Binding contains:

- Track ID
- `Renderer`
- Material Index

Bindings support both `MeshRenderer` and `SkinnedMeshRenderer` through Unity's
common `Renderer` API.

Atlas properties are applied with:

```csharp
renderer.SetPropertyBlock(block, materialIndex);
```

This supports:

- Separate renderers with material index `0`
- Multiple tracks on one shared renderer
- Bone-driven meshes using `SkinnedMeshRenderer`

## Material Properties

Face shaders must expose:

```text
_Tiling
_Offset
```

The controller calculates:

```text
Tiling X = 1 / Columns
Tiling Y = 1 / Rows

Offset X = Column / Columns
Offset Y = (Rows - 1 - Row) / Rows
```

Material instances must not be created at runtime. Updates use a cached
`MaterialPropertyBlock` per binding.

## Validation

Validation reports:

- Empty or duplicate Track IDs
- Missing atlas textures
- Invalid grid sizes
- Sprite coordinates outside the grid
- Empty or duplicate Sprite IDs
- Missing semantic Frame IDs
- Empty Default Frame IDs
- Default Frame IDs missing from their Track State mappings
- Missing Sprite IDs
- Missing library tracks
- Missing renderers
- Invalid material indices
- Invalid animation durations

Missing data is not guessed and does not silently fall back to another track.

## Transparency and Render Order

Transparent facial materials may be sorted differently from physical mesh
depth. Explicit material Render Queue values can provide stable ordering:

```text
Eyes Material  = 3000
Mouth Material = 3010
```

The correct order depends on the character. Render Queue ordering is often
preferable to moving meshes solely to influence transparent sorting.

## Shader Support

Available shader implementations:

- URP: `Assets/Shader/FaceAtlasShader.shadergraph`
- Built-in Render Pipeline: `Assets/Shaders/FaceAtlasBuiltIn.shader`

Built-in projects should use `Face/Atlas Built-In` instead of converting the
project to URP.
