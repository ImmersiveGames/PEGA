using System;
using System.Collections.Generic;
using UnityEngine;
using FrameResult = FaceAnimation.FrameResult;

public sealed class FaceController : MonoBehaviour
{
    private static readonly int OffsetPropertyId = Shader.PropertyToID("_Offset");
    private static readonly int TilingPropertyId = Shader.PropertyToID("_Tiling");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int MainTextPropertyId = Shader.PropertyToID("_MainText");

    [SerializeField]
    private bool debugLogging;

    [SerializeField]
    private List<TrackBinding> trackBindings = new List<TrackBinding>();

    [SerializeField]
    private List<FaceState> states = new List<FaceState>();

    [SerializeField]
    private List<FaceAnimation> animations = new List<FaceAnimation>();

    private readonly List<FaceAnimationPlayer> activePlayers = new List<FaceAnimationPlayer>();
    private readonly Dictionary<string, FrameResult> resolvedFrames = new Dictionary<string, FrameResult>();
    private Dictionary<string, TrackBinding> bindingLookup;
    private Dictionary<string, FaceAnimation> animationLookup;
    private Dictionary<FaceAnimation, float> intervalTimers;
    private Dictionary<string, FaceState> stateLookup;
    private FaceState currentState;

    private void OnEnable()
    {
        BuildLookups();
        InitializeDefaultState();
    }

    private void OnValidate()
    {
        BuildLookups();
        ValidateConfiguration();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        UpdateActivePlayers(deltaTime);
        UpdateIntervalAnimations(deltaTime);
        ResolveAndApplyTracks();
    }

    public void SetState(string stateId)
    {
        if (string.IsNullOrWhiteSpace(stateId)
            || stateLookup == null
            || !stateLookup.TryGetValue(stateId, out FaceState state))
        {
            return;
        }

        currentState = state;

        if (debugLogging)
        {
            Debug.Log($"Applying state: {stateId}", this);
        }

        StopAllAnimations();
        ResetIntervalTimers();
        ResolveAndApplyTracks();
    }

    public void PlayAnimation(string animationId)
    {
        if (string.IsNullOrWhiteSpace(animationId)
            || animationLookup == null
            || !animationLookup.TryGetValue(animationId, out FaceAnimation animation))
        {
            return;
        }

        StopAnimationTracks(animation);

        if (debugLogging)
        {
            Debug.Log($"Playing animation: {animationId}", this);
        }

        FaceAnimationPlayer player = new FaceAnimationPlayer();

        if (player.Start(animation))
        {
            activePlayers.Add(player);
        }
    }

    public void StopAnimation(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return;
        }

        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            activePlayers[i].StopTrack(trackId);

            if (!activePlayers[i].IsPlaying)
            {
                activePlayers.RemoveAt(i);
            }
        }
    }

    public void StopAllAnimations()
    {
        activePlayers.Clear();
    }

    private void BuildLookups()
    {
        BuildBindingLookup();
        BuildStateLookup();
        BuildAnimationLookup();
    }

    private void BuildBindingLookup()
    {
        bindingLookup = new Dictionary<string, TrackBinding>();

        if (trackBindings == null)
        {
            return;
        }

        for (int i = 0; i < trackBindings.Count; i++)
        {
            TrackBinding binding = trackBindings[i];

            if (binding == null)
            {
                Debug.LogWarning($"Track Binding {i} is null.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.TrackId))
            {
                Debug.LogWarning($"Track Binding {i} has an empty Track ID.", this);
                continue;
            }

            if (bindingLookup.ContainsKey(binding.TrackId))
            {
                Debug.LogWarning($"Duplicate Track Binding ID '{binding.TrackId}'.", this);
                continue;
            }

            if (!binding.ResolveRuntimeRenderer(transform, out string resolutionError))
            {
                Debug.LogWarning(resolutionError, this);
            }

            bindingLookup.Add(binding.TrackId, binding);
        }
    }

    private void BuildStateLookup()
    {
        stateLookup = new Dictionary<string, FaceState>();

        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            FaceState state = states[i];

            if (state == null
                || string.IsNullOrWhiteSpace(state.Id)
                || stateLookup.ContainsKey(state.Id))
            {
                continue;
            }

            stateLookup.Add(state.Id, state);
        }
    }

    private void BuildAnimationLookup()
    {
        animationLookup = new Dictionary<string, FaceAnimation>();
        intervalTimers = new Dictionary<FaceAnimation, float>();

        if (animations == null)
        {
            return;
        }

        for (int i = 0; i < animations.Count; i++)
        {
            FaceAnimation animation = animations[i];

            if (animation == null
                || string.IsNullOrWhiteSpace(animation.Id)
                || animationLookup.ContainsKey(animation.Id))
            {
                continue;
            }

            animationLookup.Add(animation.Id, animation);

            if (animation.Mode == FaceAnimation.PlaybackMode.Interval)
            {
                intervalTimers.Add(animation, 0f);
            }
        }
    }

    private void InitializeDefaultState()
    {
        currentState = null;

        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != null && !string.IsNullOrWhiteSpace(states[i].Id))
            {
                currentState = states[i];
                ResolveAndApplyTracks();
                return;
            }
        }
    }

    private void ResolveAndApplyTracks()
    {
        resolvedFrames.Clear();

        if (currentState != null)
        {
            IReadOnlyList<FaceState.TrackState> stateTracks = currentState.TrackStates;

            for (int i = 0; i < stateTracks.Count; i++)
            {
                FaceState.TrackState trackState = stateTracks[i];

                if (trackState == null
                    || !currentState.TryGetBaseCoordinate(trackState.TrackId, out Vector2Int coordinate))
                {
                    if (debugLogging && trackState != null)
                    {
                        Debug.LogWarning(
                            $"Could not resolve default frame '{trackState.DefaultFrameId}' "
                            + $"for track '{trackState.TrackId}' in state '{currentState.Id}'.",
                            this);
                    }

                    continue;
                }

                currentState.TryGetSpriteId(
                    trackState.TrackId,
                    trackState.DefaultFrameId,
                    out string spriteId);

                resolvedFrames[trackState.TrackId] = new FrameResult
                {
                    TrackId = trackState.TrackId,
                    Coordinate = coordinate,
                    GridSize = currentState.SpriteLibrary.TryGetTrack(
                        trackState.TrackId,
                        out FaceSpriteLibrary.SpriteTrack spriteTrack)
                            ? spriteTrack.GridSize
                            : Vector2Int.zero,
                    Atlas = spriteTrack != null ? spriteTrack.Atlas : null
                };

                if (debugLogging)
                {
                    Debug.Log(
                        $"Resolved track '{trackState.TrackId}': default frame "
                        + $"'{trackState.DefaultFrameId}', sprite '{spriteId}', coordinate {coordinate}.",
                        this);
                }
            }
        }

        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].CollectFrames(currentState, resolvedFrames);
        }

        foreach (KeyValuePair<string, FrameResult> pair in resolvedFrames)
        {
            ApplyTrack(pair.Key, pair.Value);
        }
    }

    private void ApplyTrack(string trackId, FrameResult frame)
    {
        if (bindingLookup == null
            || !bindingLookup.TryGetValue(trackId, out TrackBinding binding))
        {
            if (debugLogging)
            {
                Debug.LogWarning($"No Track Binding was found for Track ID '{trackId}'.", this);
            }

            return;
        }

        bool applied = binding.Apply(frame.Coordinate, frame.GridSize, frame.Atlas);

        if (debugLogging)
        {
            Debug.Log(
                applied
                    ? $"Applied track '{trackId}' to renderer '{binding.RendererName}', "
                        + $"material index {binding.MaterialIndex}, coordinate {frame.Coordinate}, "
                        + $"grid {frame.GridSize}."
                    : $"Failed to apply track '{trackId}' to its binding.",
                this);
        }
    }

    private void UpdateActivePlayers(float deltaTime)
    {
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            activePlayers[i].Update(deltaTime);

            if (!activePlayers[i].IsPlaying)
            {
                activePlayers.RemoveAt(i);
            }
        }
    }

    private void UpdateIntervalAnimations(float deltaTime)
    {
        if (animations == null || intervalTimers == null)
        {
            return;
        }

        for (int i = 0; i < animations.Count; i++)
        {
            FaceAnimation animation = animations[i];

            if (animation == null
                || animation.Mode != FaceAnimation.PlaybackMode.Interval
                || animation.Interval <= 0f
                || !intervalTimers.TryGetValue(animation, out float timer))
            {
                continue;
            }

            timer += deltaTime;

            if (timer < animation.Interval)
            {
                intervalTimers[animation] = timer;
                continue;
            }

            intervalTimers[animation] = 0f;

            if (!IsAnyTrackPlaying(animation))
            {
                PlayAnimation(animation.Id);
            }
        }
    }

    private bool IsAnyTrackPlaying(FaceAnimation animation)
    {
        IReadOnlyList<FaceAnimation.AnimationTrack> tracks = animation.Tracks;

        for (int i = 0; i < tracks.Count; i++)
        {
            FaceAnimation.AnimationTrack track = tracks[i];

            if (track != null && track.Enabled && IsTrackPlaying(track.TrackId))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTrackPlaying(string trackId)
    {
        for (int i = 0; i < activePlayers.Count; i++)
        {
            if (activePlayers[i].IsTrackPlaying(trackId))
            {
                return true;
            }
        }

        return false;
    }

    private void StopAnimationTracks(FaceAnimation animation)
    {
        IReadOnlyList<FaceAnimation.AnimationTrack> tracks = animation.Tracks;

        for (int i = 0; i < tracks.Count; i++)
        {
            FaceAnimation.AnimationTrack track = tracks[i];

            if (track != null && track.Enabled)
            {
                StopAnimation(track.TrackId);
            }
        }
    }

    private void ResetIntervalTimers()
    {
        if (intervalTimers == null)
        {
            return;
        }

        List<FaceAnimation> keys = new List<FaceAnimation>(intervalTimers.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            intervalTimers[keys[i]] = 0f;
        }
    }

    private void ValidateConfiguration()
    {
#if UNITY_EDITOR
        List<string> errors = new List<string>();
        ValidateBindings(errors);
        ValidateAssetList(states, "state", state => state != null ? state.Id : null, errors);
        ValidateAssetList(animations, "animation", animation => animation != null ? animation.Id : null, errors);

        if (errors.Count > 0)
        {
            Debug.LogWarning($"{name} validation warnings:\n- {string.Join("\n- ", errors)}", this);
        }
#endif
    }

    private void ValidateBindings(List<string> errors)
    {
        if (trackBindings == null || trackBindings.Count == 0)
        {
            errors.Add("Track Binding list is empty.");
            return;
        }

        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < trackBindings.Count; i++)
        {
            TrackBinding binding = trackBindings[i];

            if (binding == null)
            {
                errors.Add($"Track Binding {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.TrackId))
            {
                errors.Add($"Track Binding {i} has an empty Track ID.");
            }
            else if (!ids.Add(binding.TrackId))
            {
                errors.Add($"Duplicate Track Binding ID '{binding.TrackId}'.");
            }

            binding.Validate(errors);
        }
    }

    private static void ValidateAssetList<T>(
        List<T> assets,
        string label,
        Func<T, string> getId,
        List<string> errors)
    {
        if (assets == null)
        {
            errors.Add($"{label} list is null.");
            return;
        }

        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < assets.Count; i++)
        {
            string id = getId(assets[i]);

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{label} entry {i} is null or has an invalid ID.");
            }
            else if (!ids.Add(id))
            {
                errors.Add($"Duplicate {label} ID '{id}'.");
            }
        }
    }

    [Serializable]
    public sealed class TrackBinding
    {
        [SerializeField]
        private string trackId;

        [SerializeField]
        private Renderer targetRenderer;

        [SerializeField]
        [Min(0)]
        private int materialIndex;

        [NonSerialized]
        private MaterialPropertyBlock propertyBlock;

        [NonSerialized]
        private Renderer runtimeRenderer;

        public string TrackId => trackId;
        public string RendererName => Renderer != null ? Renderer.name : "<missing>";
        public int MaterialIndex => materialIndex;

        private Renderer Renderer => runtimeRenderer != null ? runtimeRenderer : targetRenderer;

        public bool ResolveRuntimeRenderer(Transform controllerRoot, out string error)
        {
            error = null;
            runtimeRenderer = null;

            if (targetRenderer == null)
            {
                error = $"Track Binding '{trackId}' is missing its Renderer.";
                return false;
            }

            if (targetRenderer.gameObject.scene.IsValid())
            {
                runtimeRenderer = targetRenderer;
                return true;
            }

            if (controllerRoot == null)
            {
                error = $"Track Binding '{trackId}' cannot resolve its runtime Renderer.";
                return false;
            }

            Renderer[] candidates = controllerRoot.GetComponentsInChildren<Renderer>(true);
            Renderer match = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                Renderer candidate = candidates[i];

                if (candidate.name != targetRenderer.name
                    || candidate.GetType() != targetRenderer.GetType())
                {
                    continue;
                }

                if (match != null)
                {
                    error =
                        $"Track Binding '{trackId}' matches multiple runtime Renderers named "
                        + $"'{targetRenderer.name}'. Assign a unique Renderer.";
                    return false;
                }

                match = candidate;
            }

            if (match == null)
            {
                error =
                    $"Track Binding '{trackId}' could not find runtime Renderer "
                    + $"'{targetRenderer.name}' under '{controllerRoot.name}'.";
                return false;
            }

            runtimeRenderer = match;
            return true;
        }

        public bool Apply(Vector2Int coordinate, Vector2Int gridSize, Texture atlas)
        {
            Renderer renderer = Renderer;

            if (renderer == null
                || materialIndex < 0
                || materialIndex >= renderer.sharedMaterials.Length
                || gridSize.x <= 0
                || gridSize.y <= 0)
            {
                return false;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock, materialIndex);

            if (atlas != null)
            {
                propertyBlock.SetTexture(MainTexPropertyId, atlas);
                propertyBlock.SetTexture(MainTextPropertyId, atlas);
            }

            propertyBlock.SetVector(
                TilingPropertyId,
                new Vector2(1f / gridSize.x, 1f / gridSize.y));
            propertyBlock.SetVector(
                OffsetPropertyId,
                new Vector2(
                    (float)coordinate.x / gridSize.x,
                    (float)(gridSize.y - 1 - coordinate.y) / gridSize.y));

            renderer.SetPropertyBlock(propertyBlock, materialIndex);
            return true;
        }

        public void Validate(List<string> errors)
        {
            if (targetRenderer == null)
            {
                errors.Add($"Track Binding '{trackId}' is missing its Renderer.");
                return;
            }

            if (materialIndex < 0 || materialIndex >= targetRenderer.sharedMaterials.Length)
            {
                errors.Add(
                    $"Track Binding '{trackId}' has invalid Material Index {materialIndex} "
                    + $"for Renderer '{targetRenderer.name}'.");
            }
        }
    }
}
