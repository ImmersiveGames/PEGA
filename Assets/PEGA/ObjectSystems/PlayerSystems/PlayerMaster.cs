using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public sealed class PlayerMaster : ObjectMaster
    {

        internal readonly PlayerData PlayerData;
        #region Delegates
        public PlayerMaster(PlayerData objectData) : base(objectData)
        {
            PlayerData = objectData;
        }
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