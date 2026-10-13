# Face System User Manual

## Introduction

The Face System is a lightweight atlas facial animation framework for Unity.
It uses dynamic Track IDs rather than fixed facial parts.

Example Track IDs:

```text
eyes
mouth
eyebrows
tears
fx
```

New tracks are created entirely through assets and inspector configuration.
No runtime code changes are required.

## Workflow

```text
Atlas textures
    |
FaceSpriteLibrary tracks
    |
FaceState track states
    |
FaceAnimation tracks
    |
FaceController bindings
    |
Renderer material slots
```

## Preparing Atlases

Each track uses a regular grid atlas. Every sprite must occupy one cell.

```text
Tiling X = 1 / Columns
Tiling Y = 1 / Rows
```

Keep cells evenly sized and aligned. Separate tracks may use different atlas
textures and grid sizes.

## Creating a FaceSpriteLibrary

Create:

```text
Create > Face System > Face Sprite Library
```

Add one entry to `Tracks` for every facial layer.

For each track configure:

- Track ID
- Atlas texture
- Grid size
- Sprite IDs and atlas coordinates

Example:

```text
Track ID: eyebrows
Atlas: EyebrowExpressions
Grid Size: 4 x 2

eyebrow.normal -> (0, 0)
eyebrow.angry  -> (1, 0)
eyebrow.sad    -> (2, 0)
```

Track IDs and Sprite IDs are case-sensitive string contracts.

## Creating FaceStates

Create:

```text
Create > Face System > Face State
```

Assign the sprite library, then add Track States.

Each Track State contains:

- Track ID matching a library track
- Default Frame ID
- Semantic Frame ID to Sprite ID mappings

Example:

```text
Track ID: eyes

opened -> eye.normal.open
half   -> eye.normal.half
closed -> eye.normal.closed
```

Default Frame ID selects the persistent appearance for that track:

```text
Default Frame ID: opened
```

It must match one of the semantic Frame IDs in the mapping list. Reordering
the mappings does not change the resting appearance.

## Creating FaceAnimations

Create:

```text
Create > Face System > Face Animation
```

Add only the tracks the animation should override.

Each Animation Track contains:

- Track ID
- Enabled toggle
- Frame Source
- Timed frame list

### FromState

`FromState` uses semantic Frame IDs from the current state.

Example blink:

```text
Track ID: eyes

half
closed
half
opened
```

Assign a Template State for inspector validation. Runtime resolution always
uses the controller's active state.

### CustomLibrary

`CustomLibrary` references Sprite IDs directly from the animation's library.
Use it for visuals that should ignore the current state, such as special FX or
look-around frames.

## Configuring FaceController

Add `FaceController` to the character and configure:

- Track Bindings
- States
- Animations

Each Track Binding connects:

```text
Track ID -> Renderer -> Material Index
```

### Separate Meshes

```text
eyes  -> EyesQuad Renderer  -> index 0
mouth -> MouthQuad Renderer -> index 0
```

### Shared Renderer

A single `MeshRenderer` or `SkinnedMeshRenderer` can expose several materials:

```text
0 Skin
1 Eyes
2 Mouth
3 Eyebrows
```

Bindings:

```text
eyes     -> HeadRenderer -> index 1
mouth    -> HeadRenderer -> index 2
eyebrows -> HeadRenderer -> index 3
```

Material Index must exist in the renderer's material list.

## MeshRenderer and SkinnedMeshRenderer

Bindings use Unity's common `Renderer` API and support:

- `MeshRenderer` for static meshes
- `SkinnedMeshRenderer` for rigged or bone-driven meshes

Both use the same indexed `MaterialPropertyBlock` runtime path.

## Runtime API

Set the active state:

```csharp
faceController.SetState("fs.angry.state");
```

Play an animation:

```csharp
faceController.PlayAnimation("fa.blink");
```

Stop animation overrides for one dynamic track:

```csharp
faceController.StopAnimation("eyes");
```

Stop all animations:

```csharp
faceController.StopAllAnimations();
```

## FaceControllerTester

Add `FaceControllerTester` beside the controller. In Play Mode its inspector
creates buttons for registered states and animations.

## Shader Setup

Shader options:

- URP: `Assets/Shader/FaceAtlasShader.shadergraph`
- Built-in Render Pipeline: `Assets/Shaders/FaceAtlasBuiltIn.shader`

Face materials must expose `_Tiling` and `_Offset`. The controller updates
these properties through `MaterialPropertyBlock`.

## Face Layering and Render Order

Transparent materials can render in an unexpected order when facial surfaces
are close together.

Symptoms include:

- Eyes rendering over the mouth
- Mouth rendering over the eyes
- Unstable layering between transparent tracks

Set explicit Render Queue values when needed:

```text
Eyes Material  = 3000
Mouth Material = 3010
```

Higher values render later. Reverse the values when the desired layering is
reversed. Exact values depend on the character.

## Troubleshooting

### A Track Does Not Render

Check:

- Track ID matches across library, state, animation, and binding
- Binding has a renderer
- Material Index exists
- Atlas grid size is valid
- Sprite coordinate is inside the grid

### An Animation Does Not Override a Track

Check:

- Animation is registered in the controller
- Animation Track is enabled
- Track ID matches the state and binding
- `FromState` Frame ID exists in the active state
- `CustomLibrary` Sprite ID exists in the selected library

### Wrong Base Frame Appears

Check that Default Frame ID points to the intended semantic mapping. The
mapping order does not control the base frame.

## V1 Migration

V2 intentionally removes the fixed Eyes/Mouth data model.

Migration mapping:

```text
eyesAtlas + eyes entries   -> library track "eyes"
mouthAtlas + mouth entries -> library track "mouth"

FaceState.eyes  -> Track State "eyes"
FaceState.mouth -> Track State "mouth"

FaceAnimation.eyes  -> Animation Track "eyes"
FaceAnimation.mouth -> Animation Track "mouth"

eyesPart / mouthPart -> Track Bindings
```

`FacePart` is no longer used. Configure the renderer and material index
directly in `FaceController`.

Existing V2 state assets created before explicit defaults are migrated by
setting Default Frame ID to their previous first mapping, preserving their
visual behavior.
