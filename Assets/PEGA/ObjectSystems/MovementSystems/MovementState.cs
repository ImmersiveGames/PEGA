namespace PEGA.ObjectSystems.MovementSystems
{
    public abstract class MovementState
    {
        public bool IsGrounded { get; set; }
        public bool IsJumpPressed { get; set; }
        public bool IsFallingFromJump { get; set; }
        public bool IsFallingFromFreeFall { get; set; }
        public float VerticalVelocity { get; set; }
    }
}