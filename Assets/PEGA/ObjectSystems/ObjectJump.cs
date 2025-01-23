using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    [RequireComponent(typeof(CharacterController))]
    public abstract class ObjectJump : MonoBehaviour, IObjectAction
    {
        protected ObjectMaster ObjectMaster;
        private CharacterController _characterController;
        
        #region UNity Methods

        private void Awake()
        {
            SetInitialReferences();
        }
        

        #endregion
        protected virtual void SetInitialReferences()
        {
            ObjectMaster = GetComponent<ObjectMaster>();
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                Debug.LogError($"CharacterController NÃO encontrado no GameObject '{gameObject.name}'.");
            }
            else
            {
                Debug.Log($"CharacterController encontrado no GameObject '{gameObject.name}'.");
            }
        }
        public void ExecuteAction()
        {
            AttemptJump();
        }

        protected virtual void AttemptJump()
        {
            DebugManager.Log<ObjectJump>($"JUMP");
        }
    }
}