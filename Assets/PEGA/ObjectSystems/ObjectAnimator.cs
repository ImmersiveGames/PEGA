using PEGA.ObjectSystems.Tags;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    public class ObjectAnimator : MonoBehaviour
    {
        private Animator _animator;
        private ObjectMaster _objectMaster;

        #region Unity Methods

        private void OnEnable()
        {
            _objectMaster = GetComponent<ObjectMaster>();
            _objectMaster.CreateSkin += SetSkinAnimator;
            SetSkinAnimator();
        }

        private void OnDisable()
        {
            _objectMaster.CreateSkin -= SetSkinAnimator;
        }

        #endregion

        private void SetSkinAnimator()
        {
            var skin = GetComponentInChildren<SkinAttach>();
            _animator = skin.GetComponentInChildren<Animator>();
        }

        #region Animator Proxy

        public void SetFloat(string parameterName, float value)
        {
            _animator?.SetFloat(parameterName, value);
        }

        public void SetBool(string parameterName, bool value)
        {
            _animator?.SetBool(parameterName, value);
        }

        public void SetTrigger(string parameterName)
        {
            _animator?.SetTrigger(parameterName);
        }

        #endregion
    }
}