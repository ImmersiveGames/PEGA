# Face System Documentation

The Face System V2 is a generic, dynamic track-based atlas animation framework.
The authoritative architecture is documented in
`Assets/Docs/FACESYSTEM_ARCHITECTURE_V2.md`.

## Core Assets

- `FaceSpriteLibrary`: atlas data grouped by user-defined Track ID.
- `FaceState`: persistent semantic mappings grouped by Track ID.
- `FaceAnimation`: temporary timed overrides grouped by Track ID.
- `FaceController`: state, animation, and renderer binding orchestration.
- `FaceControllerTester`: Play Mode state and animation controls.

There are no built-in Eyes or Mouth runtime paths. Those names are ordinary
Track IDs used by the sample content.

## Rendering

Each controller binding connects:

```text
Track ID -> Renderer -> Material Index
```

The controller uses:

```csharp
renderer.SetPropertyBlock(block, materialIndex);
```

This supports separate meshes, shared renderers, `MeshRenderer`, and
`SkinnedMeshRenderer`.

## Persistent State

Each state contains Track States with an explicit Default Frame ID. The
default resolves through that track's semantic mappings and remains stable
when mappings are reordered. Active animations override only their own tracks,
and tracks return naturally to the default frame when playback ends.

## Authoring

See `face_system_user_manual.md` for setup, examples, troubleshooting, shader
selection, transparency sorting, and V1 migration instructions.
