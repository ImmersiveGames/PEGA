using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    [RequireComponent(typeof(CharacterController))]
    public abstract class ObjectJump : MonoBehaviour, IObjectAction
    {
        protected ObjectMaster ObjectMaster;
        protected CharacterController CharacterController;
        
        #region UNity Methods
        protected virtual void OnEnable()
        {
            SetInitialReferences();
        }

        protected virtual void OnDisable()
        {
     
        }

        #endregion
        protected virtual void SetInitialReferences()
        {
            ObjectMaster = GetComponent<ObjectMaster>();
            CharacterController = GetComponent<CharacterController>();
        }
        public void ExecuteAction()
        {
            AttemptJump();
        }

        protected virtual void AttemptJump()
        {
            DebugManager.Log<ObjectJump>("JUMP");
        }
    }
}