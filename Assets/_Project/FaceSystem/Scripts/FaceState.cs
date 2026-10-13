using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Face System/Face State")]
public sealed partial class FaceState : ScriptableObject
{
    [SerializeField]
    private string id;

    [SerializeField]
    private FaceSpriteLibrary spriteLibrary;

    [SerializeField]
    private List<TrackState> trackStates = new List<TrackState>();

    private Dictionary<string, TrackState> trackLookup;
    private string lastValidationWarning;

    public string Id => id;
    public FaceSpriteLibrary SpriteLibrary => spriteLibrary;
    public IReadOnlyList<TrackState> TrackStates => trackStates;

    public bool TryGetCoordinate(string trackId, string frameId, out Vector2Int coordinate)
    {
        coordinate = Vector2Int.zero;

        return TryGetSpriteId(trackId, frameId, out string spriteId)
            && spriteLibrary != null
            && spriteLibrary.TryGetCoordinate(trackId, spriteId, out coordinate);
    }

    public bool TryGetBaseCoordinate(string trackId, out Vector2Int coordinate)
    {
        coordinate = Vector2Int.zero;

        return TryGetTrackState(trackId, out TrackState trackState)
            && trackState.TryGetDefaultSpriteId(out string spriteId)
            && spriteLibrary != null
            && spriteLibrary.TryGetCoordinate(trackId, spriteId, out coordinate);
    }

    public bool TryGetSpriteId(string trackId, string frameId, out string spriteId)
    {
        spriteId = null;

        return TryGetTrackState(trackId, out TrackState trackState)
            && trackState.TryGetSpriteId(frameId, out spriteId);
    }

    public bool HasFrame(string trackId, string frameId)
    {
        return TryGetTrackState(trackId, out TrackState trackState)
            && trackState.HasFrame(frameId);
    }

    public bool TryGetTrackState(string trackId, out TrackState trackState)
    {
        EnsureLookup();
        trackState = null;

        return !string.IsNullOrWhiteSpace(trackId)
            && trackLookup != null
            && trackLookup.TryGetValue(trackId, out trackState);
    }

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        List<string> errors = new List<string>();

#if UNITY_EDITOR
        if (string.IsNullOrEmpty(id) || id != name)
        {
            id = name;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        BuildLookup();

        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("ID is invalid.");
        }

        if (spriteLibrary == null)
        {
            errors.Add("Sprite library is missing.");
        }

        ValidateTrackStates(errors);
        LogValidation(errors);
    }

    private void EnsureLookup()
    {
        if (trackLookup == null)
        {
            BuildLookup();
        }
    }

    private void BuildLookup()
    {
        trackLookup = new Dictionary<string, TrackState>();

        if (trackStates == null)
        {
            return;
        }

        for (int i = 0; i < trackStates.Count; i++)
        {
            TrackState trackState = trackStates[i];

            if (trackState == null
                || string.IsNullOrWhiteSpace(trackState.TrackId)
                || trackLookup.ContainsKey(trackState.TrackId))
            {
                continue;
            }

            trackState.BuildLookup();
            trackLookup.Add(trackState.TrackId, trackState);
        }
    }

    private void ValidateTrackStates(List<string> errors)
    {
        if (trackStates == null || trackStates.Count == 0)
        {
            errors.Add("Track State list is empty.");
            return;
        }

        HashSet<string> trackIds = new HashSet<string>();

        for (int i = 0; i < trackStates.Count; i++)
        {
            TrackState trackState = trackStates[i];
            string label = $"Track State {i}";

            if (trackState == null)
            {
                errors.Add($"{label} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(trackState.TrackId))
            {
                errors.Add($"{label} has an empty Track ID.");
            }
            else
            {
                label = $"Track State '{trackState.TrackId}'";

                if (!trackIds.Add(trackState.TrackId))
                {
                    errors.Add($"Duplicate Track ID '{trackState.TrackId}'.");
                }

                if (spriteLibrary != null && !spriteLibrary.TryGetTrack(trackState.TrackId, out _))
                {
                    errors.Add($"{label} references a missing library track.");
                }
            }

            trackState.Validate(label, spriteLibrary, errors);
        }
    }

    private void LogValidation(List<string> errors)
    {
        if (errors.Count == 0)
        {
            lastValidationWarning = null;
            return;
        }

        string warning = $"{name} validation warnings:\n- {string.Join("\n- ", errors)}";

        if (warning != lastValidationWarning)
        {
            Debug.LogWarning(warning, this);
            lastValidationWarning = warning;
        }
    }
}
