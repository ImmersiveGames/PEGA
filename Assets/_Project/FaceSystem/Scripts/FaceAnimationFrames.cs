using System;
using UnityEngine;

public sealed partial class FaceAnimation
{
    [Serializable]
    public struct StateFrame
    {
        [SerializeField]
        private string frameId;

        [SerializeField]
        private float duration;

        public string FrameId => frameId;
        public float Duration => duration;
    }

    [Serializable]
    public struct SpriteFrame
    {
        [SerializeField]
        private string spriteId;

        [SerializeField]
        private float duration;

        public string SpriteId => spriteId;
        public float Duration => duration;
    }
}
