using System;
using ImmersiveGames.InputSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 *
 * TODO: Melhorar essa parte de mensagens e testar. só fiz para não ficar dando erro
 * var playerInputHandler = player.GetComponent<PlayerInputHandler>();
 * var notificationPanel = Instantiate(notificationPanelPrefab, player.transform);
 * notificationPanel.Initialize(playerInputHandler);
 * notificationPanel.Show("Mensagem de teste", OnCloseCallback, OnConfirmCallback);
 */

namespace ImmersiveGames.NotificationsSystems
{
    public class NotificationPanel : MonoBehaviour
    {
        public TMP_Text messageText;
        public Button confirmButton;

        public float openTimeDuration = 0.3f;
        public float closeTimeDuration = 0.3f;

        private Animator _animator;
        private AudioSource _audioSource;

        private static readonly int OpenTrigger = Animator.StringToHash("OpenNotification");
        private static readonly int CloseTrigger = Animator.StringToHash("CloseNotification");

        private Action _onCloseCallback;
        private Action _onConfirmCallback;

        // Referência ao PlayerInputHandler do jogador associado
        private PlayerInputHandler _playerInputHandler;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _audioSource = GetComponent<AudioSource>();
        }

        public void Initialize(PlayerInputHandler playerInputHandler)
        {
            _playerInputHandler = playerInputHandler;
        }

        public void Show(string message, Action onClose, Action onConfirm = null)
        {
            _onCloseCallback = onClose;
            _onConfirmCallback = onConfirm;

            PlayNotificationSound();
            SetMessageText(message);
            ConfigureButtons();
            OpenPanel();

            if (_onConfirmCallback != null && _playerInputHandler != null)
            {
                _playerInputHandler.ActionManager.RegisterAction("ConfirmNotification", ConfirmActionHandler);
            }
        }

        private void PlayNotificationSound()
        {
            _audioSource?.Play();
        }

        private void SetMessageText(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        private void ConfigureButtons()
        {
            if (confirmButton == null) return;

            confirmButton.gameObject.SetActive(_onConfirmCallback != null);

            if (_onConfirmCallback == null) return;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                _onConfirmCallback.Invoke();
                ClosePanel();
            });
        }

        private void OpenPanel()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(OpenTrigger);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void ClosePanel()
        {
            if (_onConfirmCallback != null && _playerInputHandler != null)
            {
                _playerInputHandler.ActionManager.UnregisterAction("ConfirmNotification", ConfirmActionHandler);
            }

            if (_animator != null)
            {
                _animator.SetTrigger(CloseTrigger);
                StartCoroutine(DelayedClose(_animator.GetCurrentAnimatorStateInfo(0).length));
            }
            else
            {
                gameObject.SetActive(false);
                _onCloseCallback?.Invoke();
            }
        }

        private void ConfirmActionHandler(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            confirmButton?.onClick?.Invoke();
        }

        private System.Collections.IEnumerator DelayedClose(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
            _onCloseCallback?.Invoke();
        }
    }
}
