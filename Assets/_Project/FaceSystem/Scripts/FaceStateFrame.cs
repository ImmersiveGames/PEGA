using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FaceState
{
    [Serializable]
    public sealed class TrackState
    {
        [SerializeField]
        private string trackId;

        [SerializeField]
        private string defaultFrameId;

        [SerializeField]
        private List<StateFrame> frames = new List<StateFrame>();

        private Dictionary<string, string> frameLookup;

        public string TrackId => trackId;
        public string DefaultFrameId => defaultFrameId;
        public IReadOnlyList<StateFrame> Frames => frames;

        public bool HasFrame(string frameId)
        {
            EnsureLookup();
            return !string.IsNullOrWhiteSpace(frameId) && frameLookup.ContainsKey(frameId);
        }

        public bool TryGetSpriteId(string frameId, out string spriteId)
        {
            EnsureLookup();
            spriteId = null;

            return !string.IsNullOrWhiteSpace(frameId)
                && frameLookup.TryGetValue(frameId, out spriteId);
        }

        public bool TryGetDefaultSpriteId(out string spriteId)
        {
            return TryGetSpriteId(defaultFrameId, out spriteId);
        }

        internal void BuildLookup()
        {
            frameLookup = new Dictionary<string, string>();

            if (frames == null)
            {
                return;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                StateFrame frame = frames[i];

                if (string.IsNullOrWhiteSpace(frame.FrameId) || frameLookup.ContainsKey(frame.FrameId))
                {
                    continue;
                }

                frameLookup.Add(frame.FrameId, frame.SpriteId);
            }
        }

        internal void Validate(string label, FaceSpriteLibrary library, List<string> errors)
        {
            if (frames == null || frames.Count == 0)
            {
                errors.Add($"{label} has no frame mappings.");
                return;
            }

            HashSet<string> frameIds = new HashSet<string>();

            for (int i = 0; i < frames.Count; i++)
            {
                StateFrame frame = frames[i];

                if (string.IsNullOrWhiteSpace(frame.FrameId))
                {
                    errors.Add($"{label} frame {i} has an empty semantic Frame ID.");
                }
                else if (!frameIds.Add(frame.FrameId))
                {
                    errors.Add($"{label} has duplicate Frame ID '{frame.FrameId}'.");
                }

                if (string.IsNullOrWhiteSpace(frame.SpriteId))
                {
                    errors.Add($"{label} frame '{frame.FrameId}' has an empty Sprite ID.");
                }
                else if (library != null
                    && !string.IsNullOrWhiteSpace(trackId)
                    && !library.TryGetCoordinate(trackId, frame.SpriteId, out _))
                {
                    errors.Add($"{label} frame '{frame.FrameId}' references missing Sprite ID '{frame.SpriteId}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(defaultFrameId))
            {
                errors.Add($"{label} has an empty Default Frame ID.");
            }
            else if (!frameIds.Contains(defaultFrameId))
            {
                errors.Add(
                    $"{label} Default Frame ID '{defaultFrameId}' does not exist in its frame mappings.");
            }
        }

        private void EnsureLookup()
        {
            if (frameLookup == null)
            {
                BuildLookup();
            }
        }
    }

    [Serializable]
    public struct StateFrame
    {
        [SerializeField]
        private string frameId;

        [SerializeField]
        private string spriteId;

        public string FrameId => frameId;
        public string SpriteId => spriteId;
    }
}
