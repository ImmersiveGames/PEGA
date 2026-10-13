using UnityEngine;

public sealed partial class FaceAnimation
{
    public enum PlaybackMode
    {
        Sequential,
        Loop,
        Interval,
        Random
    }

    public enum FrameSourceMode
    {
        FromState,
        CustomLibrary
    }

    public enum FrameDataType
    {
        StateFrame,
        SpriteFrame
    }

    public struct FrameResult
    {
        public string TrackId;
        public FrameDataType DataType;
        public string FrameId;
        public string SpriteId;
        public Vector2Int Coordinate;
        public Vector2Int GridSize;
        public Texture Atlas;
        public float Duration;
    }
}
