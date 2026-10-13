using UnityEngine;
using FrameResult = FaceAnimation.FrameResult;

internal sealed class FaceTrackPlayer
{
    private FaceAnimation animation;
    private FaceAnimation.AnimationTrack track;
    private FaceSpriteLibrary spriteLibrary;
    private int frameIndex;
    private float timeInFrame;

    public string TrackId => track != null ? track.TrackId : null;
    public bool IsPlaying { get; private set; }

    public void Start(
        FaceAnimation sourceAnimation,
        FaceAnimation.AnimationTrack sourceTrack,
        FaceSpriteLibrary sourceLibrary)
    {
        animation = sourceAnimation;
        track = sourceTrack;
        spriteLibrary = sourceLibrary;
        frameIndex = animation != null
            && animation.Mode == FaceAnimation.PlaybackMode.Random
            && track != null
            && track.FrameCount > 0
                ? Random.Range(0, track.FrameCount)
                : 0;
        timeInFrame = 0f;
        IsPlaying = animation != null && track != null && track.FrameCount > 0;
    }

    public void Update(float deltaTime)
    {
        if (!IsPlaying || animation == null)
        {
            return;
        }

        if (!TryGetFrameTiming(out FrameResult frame) || frame.Duration <= 0f)
        {
            Stop();
            return;
        }

        timeInFrame += deltaTime;

        while (IsPlaying && timeInFrame >= frame.Duration)
        {
            timeInFrame -= frame.Duration;
            AdvanceFrame();

            if (IsPlaying && (!TryGetFrameTiming(out frame) || frame.Duration <= 0f))
            {
                Stop();
            }
        }
    }

    public bool TryGetFrame(FaceState state, out FrameResult frame)
    {
        frame = default;

        if (!IsPlaying || !TryGetFrameTiming(out frame) || !ResolveCoordinate(state, ref frame))
        {
            return false;
        }

        frame.TrackId = TrackId;
        return true;
    }

    public void Stop()
    {
        animation = null;
        track = null;
        spriteLibrary = null;
        frameIndex = 0;
        timeInFrame = 0f;
        IsPlaying = false;
    }

    private bool TryGetFrameTiming(out FrameResult frame)
    {
        frame = default;

        if (track == null || !track.TryGetFrame(frameIndex, out frame))
        {
            return false;
        }

        frame.TrackId = track.TrackId;
        return true;
    }

    private bool ResolveCoordinate(FaceState state, ref FrameResult frame)
    {
        if (frame.DataType == FaceAnimation.FrameDataType.StateFrame)
        {
            if (state == null
                || state.SpriteLibrary == null
                || !state.TryGetCoordinate(TrackId, frame.FrameId, out frame.Coordinate)
                || !state.SpriteLibrary.TryGetTrack(TrackId, out FaceSpriteLibrary.SpriteTrack stateTrack))
            {
                return false;
            }

            frame.GridSize = stateTrack.GridSize;
            frame.Atlas = stateTrack.Atlas;
            return true;
        }

        if (frame.DataType != FaceAnimation.FrameDataType.SpriteFrame
            || spriteLibrary == null
            || !spriteLibrary.TryGetCoordinate(TrackId, frame.SpriteId, out frame.Coordinate)
            || !spriteLibrary.TryGetTrack(TrackId, out FaceSpriteLibrary.SpriteTrack spriteTrack))
        {
            return false;
        }

        frame.GridSize = spriteTrack.GridSize;
        frame.Atlas = spriteTrack.Atlas;
        return true;
    }

    private void AdvanceFrame()
    {
        int frameCount = track != null ? track.FrameCount : 0;

        if (frameCount <= 0)
        {
            Stop();
            return;
        }

        switch (animation.Mode)
        {
            case FaceAnimation.PlaybackMode.Random:
                int newIndex;

                do
                {
                    newIndex = Random.Range(0, frameCount);
                }
                while (newIndex == frameIndex && frameCount > 1);

                frameIndex = newIndex;
                break;

            case FaceAnimation.PlaybackMode.Loop:
                frameIndex = (frameIndex + 1) % frameCount;
                break;

            case FaceAnimation.PlaybackMode.Sequential:
            case FaceAnimation.PlaybackMode.Interval:
                frameIndex++;

                if (frameIndex >= frameCount)
                {
                    Stop();
                }
                break;

            default:
                Stop();
                break;
        }
    }
}
