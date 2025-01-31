using UnityEngine;

namespace ImmersiveGames.InputSystems.Interface
{
    public interface IControllerInput
    {
        Vector2 GetMovementDirection();
        bool IsActionPressed(string actionName);
        void SetEnabled(bool state);
    }
}