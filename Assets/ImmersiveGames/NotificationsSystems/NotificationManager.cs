using System;
using System.Collections;
using System.Collections.Generic;
using ImmersiveGames.PoolingSystems;
using ImmersiveGames.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ImmersiveGames.NotificationsSystems
{
    public sealed class NotificationManager : Singleton<NotificationManager>
    {
        [Header("Configurações")]
        public GameObject notificationPanelPrefab;
        public float notificationDisplayTime = 5f;

        private readonly Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
        private NotificationPanel _currentGlobalPanel;
        private GenericObjectPool<NotificationPanel> _panelPool;
        private GameObject _initiallySelectedObject;

        // Painéis individuais por jogador
        private readonly Dictionary<int, NotificationPanel> _playerPanels = new Dictionary<int, NotificationPanel>();

        private void Start()
        {
            _panelPool = new GenericObjectPool<NotificationPanel>(notificationPanelPrefab.GetComponentInChildren<NotificationPanel>(), transform, 5);
        }

        public void RegisterPlayerPanel(int playerId, NotificationPanel panel)
        {
            if (!_playerPanels.ContainsKey(playerId))
            {
                _playerPanels[playerId] = panel;
            }
        }

        public void UnregisterPlayerPanel(int playerId)
        {
            if (_playerPanels.ContainsKey(playerId))
            {
                _playerPanels.Remove(playerId);
            }
        }

        public void AddNotification(NotificationData notificationData)
        {
            _notificationQueue.Enqueue(notificationData);
            TryShowNextNotification();
        }

        public void AddNotificationForPlayer(int playerId, string message, Action onClose = null, Action onConfirm = null)
        {
            if (_playerPanels.TryGetValue(playerId, out var playerPanel))
            {
                playerPanel.Show(message, onClose, onConfirm);
            }
            else
            {
                Debug.LogWarning($"Player {playerId} não tem um painel registrado.");
            }
        }

        private void TryShowNextNotification()
        {
            if (!CanShowNotification()) return;
            if (_notificationQueue.Count == 1)
            {
                SaveInitialFocus();
            }

            var nextNotification = _notificationQueue.Dequeue();
            InstantiateNotificationPanel(nextNotification);
            _currentGlobalPanel.Show(nextNotification.message, () => CloseNotification(nextNotification));

            StartCoroutine(AutoCloseNotification(_currentGlobalPanel, notificationDisplayTime));
        }

        private bool CanShowNotification()
        {
            return _currentGlobalPanel == null && _notificationQueue.Count > 0;
        }

        private void InstantiateNotificationPanel(NotificationData nextNotification)
        {
            if (nextNotification.panelPrefab == null)
            {
                nextNotification.panelPrefab = notificationPanelPrefab;
            }
            _currentGlobalPanel = _panelPool.GetObject();
            _currentGlobalPanel.transform.SetParent(transform, false);
        }

        private void CloseNotification(NotificationData notification)
        {
            _currentGlobalPanel.ClosePanel();
            _currentGlobalPanel = null;
            if (_notificationQueue.Count == 0)
            {
                RestoreInitialFocus();
            }
        }

        private IEnumerator AutoCloseNotification(NotificationPanel panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel == _currentGlobalPanel)
            {
                panel.ClosePanel();
            }
        }

        private void SaveInitialFocus()
        {
            _initiallySelectedObject = EventSystem.current.currentSelectedGameObject;
        }

        private void RestoreInitialFocus()
        {
            if (_initiallySelectedObject != null)
            {
                EventSystem.current.SetSelectedGameObject(_initiallySelectedObject);
            }
        }
    }
}
