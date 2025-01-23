using PEGA.ObjectSystems.Tags;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    public class ObjectAnimator : MonoBehaviour
    {
        private Animator _animator;
        private ObjectMaster _objectMaster;
        private static readonly int _jump = Animator.StringToHash("Jump");
        private static readonly int _jumpEnd = Animator.StringToHash("Jump_End");
        private static readonly int _idle = Animator.StringToHash("Idle");

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

        #region Animations Calls

        public void JumpAnimation()
        {
            if (_animator)
                _animator.SetTrigger(_jump);
        }
        public void JumpEndAnimation()
        {
            if (_animator)
                _animator.SetTrigger(_jumpEnd);
        }
        public void IdleAnimation()
        {
            if (_animator)
                _animator.SetTrigger(_idle);
        }

        #endregion
    }
}