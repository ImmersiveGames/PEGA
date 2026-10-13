using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Face System/Face Sprite Library")]
public sealed partial class FaceSpriteLibrary : ScriptableObject
{
    [SerializeField]
    private List<SpriteTrack> tracks = new List<SpriteTrack>();

    private Dictionary<string, SpriteTrack> trackLookup;
    private string lastValidationWarning;

    public IReadOnlyList<SpriteTrack> Tracks => tracks;

    public bool TryGetTrack(string trackId, out SpriteTrack track)
    {
        EnsureLookup();
        track = null;

        return !string.IsNullOrWhiteSpace(trackId)
            && trackLookup != null
            && trackLookup.TryGetValue(trackId, out track);
    }

    public bool TryGetCoordinate(string trackId, string spriteId, out Vector2Int coordinate)
    {
        coordinate = Vector2Int.zero;

        return TryGetTrack(trackId, out SpriteTrack track)
            && track.TryGetCoordinate(spriteId, out coordinate);
    }

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        List<string> errors = new List<string>();
        BuildLookup();

        if (tracks == null)
        {
            errors.Add("Track list is null.");
        }
        else
        {
            HashSet<string> trackIds = new HashSet<string>();

            for (int i = 0; i < tracks.Count; i++)
            {
                SpriteTrack track = tracks[i];
                string label = $"Track {i}";

                if (track == null)
                {
                    errors.Add($"{label} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(track.TrackId))
                {
                    errors.Add($"{label} has an empty Track ID.");
                }
                else
                {
                    label = $"Track '{track.TrackId}'";

                    if (!trackIds.Add(track.TrackId))
                    {
                        errors.Add($"Duplicate Track ID '{track.TrackId}'.");
                    }
                }

                track.Validate(label, errors);
            }
        }

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
        trackLookup = new Dictionary<string, SpriteTrack>();

        if (tracks == null)
        {
            return;
        }

        for (int i = 0; i < tracks.Count; i++)
        {
            SpriteTrack track = tracks[i];

            if (track == null || string.IsNullOrWhiteSpace(track.TrackId) || trackLookup.ContainsKey(track.TrackId))
            {
                continue;
            }

            track.BuildLookup();
            trackLookup.Add(track.TrackId, track);
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
