namespace PEGA.ObjectSystems.Interfaces
{
    public interface IMovementStrategy
    {
        void Gravity(ObjectMovement context);
        void Move(ObjectMovement context);
        void Rotate(ObjectMovement context);
    }
}