using System.Collections.Generic;
using FrameResult = FaceAnimation.FrameResult;

internal sealed class FaceAnimationPlayer
{
    private readonly List<FaceTrackPlayer> trackPlayers = new List<FaceTrackPlayer>();

    public bool IsPlaying => trackPlayers.Count > 0;

    public bool Start(FaceAnimation animation)
    {
        trackPlayers.Clear();

        if (animation == null)
        {
            return false;
        }

        IReadOnlyList<FaceAnimation.AnimationTrack> tracks = animation.Tracks;

        for (int i = 0; i < tracks.Count; i++)
        {
            FaceAnimation.AnimationTrack track = tracks[i];

            if (track == null || !track.Enabled || track.FrameCount == 0)
            {
                continue;
            }

            FaceTrackPlayer player = new FaceTrackPlayer();
            player.Start(animation, track, animation.SpriteLibrary);

            if (player.IsPlaying)
            {
                trackPlayers.Add(player);
            }
        }

        return IsPlaying;
    }

    public void Update(float deltaTime)
    {
        for (int i = trackPlayers.Count - 1; i >= 0; i--)
        {
            trackPlayers[i].Update(deltaTime);

            if (!trackPlayers[i].IsPlaying)
            {
                trackPlayers.RemoveAt(i);
            }
        }
    }

    public void CollectFrames(FaceState state, Dictionary<string, FrameResult> results)
    {
        for (int i = 0; i < trackPlayers.Count; i++)
        {
            if (trackPlayers[i].TryGetFrame(state, out FrameResult frame))
            {
                results[frame.TrackId] = frame;
            }
        }
    }

    public bool IsTrackPlaying(string trackId)
    {
        for (int i = 0; i < trackPlayers.Count; i++)
        {
            if (trackPlayers[i].IsPlaying && trackPlayers[i].TrackId == trackId)
            {
                return true;
            }
        }

        return false;
    }

    public void StopTrack(string trackId)
    {
        for (int i = trackPlayers.Count - 1; i >= 0; i--)
        {
            if (trackPlayers[i].TrackId != trackId)
            {
                continue;
            }

            trackPlayers[i].Stop();
            trackPlayers.RemoveAt(i);
        }
    }
}
