using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FaceAnimation
{
    [Serializable]
    public sealed class AnimationTrack
    {
        [SerializeField]
        private string trackId;

        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private FrameSourceMode sourceMode;

        [SerializeField]
        private List<StateFrame> stateFrames = new List<StateFrame>();

        [SerializeField]
        private List<SpriteFrame> spriteFrames = new List<SpriteFrame>();

        public string TrackId => trackId;
        public bool Enabled => enabled;
        public FrameSourceMode SourceMode => sourceMode;

        public int FrameCount
        {
            get
            {
                if (!enabled)
                {
                    return 0;
                }

                return sourceMode == FrameSourceMode.FromState
                    ? (stateFrames != null ? stateFrames.Count : 0)
                    : (spriteFrames != null ? spriteFrames.Count : 0);
            }
        }

        public bool TryGetFrame(int index, out FrameResult frame)
        {
            frame = default;

            if (!enabled || index < 0)
            {
                return false;
            }

            if (sourceMode == FrameSourceMode.FromState)
            {
                return TryGetStateFrame(index, out frame);
            }

            return sourceMode == FrameSourceMode.CustomLibrary
                && TryGetSpriteFrame(index, out frame);
        }

        public void Validate(
            List<string> errors,
            string label,
            FaceState templateState,
            FaceSpriteLibrary spriteLibrary)
        {
            if (sourceMode == FrameSourceMode.FromState)
            {
                ValidateStateFrames(errors, label, templateState);
                return;
            }

            if (sourceMode == FrameSourceMode.CustomLibrary)
            {
                ValidateSpriteFrames(errors, label, spriteLibrary);
                return;
            }

            errors.Add($"{label} has an invalid frame source.");
        }

        private bool TryGetStateFrame(int index, out FrameResult frame)
        {
            frame = default;

            if (stateFrames == null || index >= stateFrames.Count)
            {
                return false;
            }

            StateFrame source = stateFrames[index];
            frame.DataType = FrameDataType.StateFrame;
            frame.FrameId = source.FrameId;
            frame.Duration = source.Duration;
            return true;
        }

        private bool TryGetSpriteFrame(int index, out FrameResult frame)
        {
            frame = default;

            if (spriteFrames == null || index >= spriteFrames.Count)
            {
                return false;
            }

            SpriteFrame source = spriteFrames[index];
            frame.DataType = FrameDataType.SpriteFrame;
            frame.SpriteId = source.SpriteId;
            frame.Duration = source.Duration;
            return true;
        }

        private void ValidateStateFrames(List<string> errors, string label, FaceState templateState)
        {
            if (stateFrames == null || stateFrames.Count == 0)
            {
                errors.Add($"{label} has no state frames.");
                return;
            }

            if (templateState == null)
            {
                errors.Add($"{label} requires a Template State for FromState validation.");
            }
            else if (!templateState.TryGetTrackState(trackId, out _))
            {
                errors.Add($"{label} references a Track ID missing from the Template State.");
            }

            for (int i = 0; i < stateFrames.Count; i++)
            {
                StateFrame frame = stateFrames[i];

                if (string.IsNullOrWhiteSpace(frame.FrameId))
                {
                    errors.Add($"{label} frame {i} has an empty semantic Frame ID.");
                }
                else if (templateState != null && !templateState.HasFrame(trackId, frame.FrameId))
                {
                    errors.Add($"{label} references missing Frame ID '{frame.FrameId}'.");
                }

                if (frame.Duration <= 0f)
                {
                    errors.Add($"{label} frame {i} has duration <= 0.");
                }
            }
        }

        private void ValidateSpriteFrames(List<string> errors, string label, FaceSpriteLibrary spriteLibrary)
        {
            if (spriteFrames == null || spriteFrames.Count == 0)
            {
                errors.Add($"{label} has no custom sprite frames.");
                return;
            }

            if (spriteLibrary == null)
            {
                errors.Add($"{label} requires a custom FaceSpriteLibrary.");
            }
            else if (!spriteLibrary.TryGetTrack(trackId, out _))
            {
                errors.Add($"{label} references a Track ID missing from the custom library.");
            }

            for (int i = 0; i < spriteFrames.Count; i++)
            {
                SpriteFrame frame = spriteFrames[i];

                if (string.IsNullOrWhiteSpace(frame.SpriteId))
                {
                    errors.Add($"{label} frame {i} has an empty Sprite ID.");
                }
                else if (spriteLibrary != null
                    && !spriteLibrary.TryGetCoordinate(trackId, frame.SpriteId, out _))
                {
                    errors.Add($"{label} references missing Sprite ID '{frame.SpriteId}'.");
                }

                if (frame.Duration <= 0f)
                {
                    errors.Add($"{label} frame {i} has duration <= 0.");
                }
            }
        }
    }
}
