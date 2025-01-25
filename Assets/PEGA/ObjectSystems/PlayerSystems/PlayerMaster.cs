using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public sealed class PlayerMaster : ObjectMaster
    {
        public int playerIndex;
        public AttributesBaseData attributesBaseData;
        internal readonly PlayerData PlayerData;
        #region Delegates
        public PlayerMaster(PlayerData objectData, AttributesBaseData attributesBaseData) : base(objectData)
        {
            PlayerData = objectData;
            this.attributesBaseData = attributesBaseData;
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