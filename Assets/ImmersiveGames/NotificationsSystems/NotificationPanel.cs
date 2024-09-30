using System;
using ImmersiveGames.InputSystems;
using ImmersiveGames.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveGames.NotificationsSystems
{
    public class NotificationPanel : MonoBehaviour
    {
        public TMP_Text messageText;
        public Button confirmButton;

        public float openTimeDuration;
        public float closeTimeDuration;

        private Animator _animator;
        private AudioSource _audioSource;

        private static readonly int NotificationTrigger = Animator.StringToHash("Notification");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (confirmButton != null)
            {
                InputGameManager.RegisterAction("ConfirmNotification", ButtonConfirm);
            }
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                InputGameManager.UnregisterAction("ConfirmNotification", ButtonConfirm);
            }
            InputGameManager.ActionManager.RestoreActionMap();
        }

        public void Show(string message, Action onClose, Action onConfirm = null)
        {
            PlayNotificationSound();
            SetMessageText(message);
            ConfigureButtons(onClose, onConfirm);
            OpenPanel();
        }

        private void PlayNotificationSound()
        {
            _audioSource?.Play();
        }

        private void SetMessageText(string message)
        {
            messageText.text = message;
        }

        private void ConfigureButtons(Action onClose, Action onConfirm)
        {
            if (confirmButton == null) return;
            confirmButton.gameObject.SetActive(onConfirm != null);
            if (onConfirm == null) return;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                onConfirm.Invoke();
                onClose?.Invoke();
                ClosePanel();
            });
        }

        private void OpenPanel()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(NotificationTrigger);
            }
            else
            {
                StartCoroutine(PanelsHelper.TogglePanel(gameObject, openTimeDuration, closeTimeDuration, true));
            }
        }

        public void ClosePanel()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(NotificationTrigger);
                var animationDuration = _animator.GetCurrentAnimatorStateInfo(0).length;
                StartCoroutine(DelayedClose(animationDuration));
            }
            else
            {
                StartCoroutine(DelayedClose(closeTimeDuration));
            }
        }

        private System.Collections.IEnumerator DelayedClose(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
            NotificationManager.instance.OnPanelClosed(this);
        }

        private void ButtonConfirm(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            confirmButton?.onClick?.Invoke();
        }
    }
}
