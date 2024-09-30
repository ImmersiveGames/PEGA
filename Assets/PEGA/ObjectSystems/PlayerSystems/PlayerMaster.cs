using UnityEngine;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public sealed class PlayerMaster : ObjectMaster
    {
        #region Delegates

        //Input System
        public delegate void AxisEventHandler(Vector2 dir);

        public event AxisEventHandler EventPlayerMasterAxisMovement;

        #endregion


        internal void OnEventPlayerMasterAxisMovement(Vector2 dir)
        {
            EventPlayerMasterAxisMovement?.Invoke(dir);
        }
    }
}