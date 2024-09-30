using System;
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
        public float notificationDisplayTime = 5f; // Tempo que cada notificação ficará visível

        private readonly Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
        private NotificationPanel _currentPanel;
        private GenericObjectPool<NotificationPanel> _panelPool;
        private GameObject _initiallySelectedObject;

        // Eventos para notificar outros scripts sobre notificações
        public event EventHandler<NotificationEventArgs> EventNotificationAdded;
        public event EventHandler<NotificationEventArgs> EventNotificationAccepted;
        public event EventHandler<NotificationEventArgs> EventNotificationClosed;

        private void Start()
        {
            _panelPool = new GenericObjectPool<NotificationPanel>(notificationPanelPrefab.GetComponentInChildren<NotificationPanel>(), transform, 5);
        }

        public void AddNotification(NotificationData notificationData)
        {
            _notificationQueue.Enqueue(notificationData);
            EventNotificationAdded?.Invoke(this, new NotificationEventArgs(notificationData));
            TryShowNextNotification();
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
            _currentPanel.Show(nextNotification.message, () => CloseNotification(nextNotification));

            // Fechar automaticamente após o tempo especificado
            StartCoroutine(AutoCloseNotification(_currentPanel, notificationDisplayTime));
        }

        private bool CanShowNotification()
        {
            return _currentPanel == null && _notificationQueue.Count > 0;
        }

        private void InstantiateNotificationPanel(NotificationData nextNotification)
        {
            if (nextNotification.panelPrefab == null)
            {
                nextNotification.panelPrefab = notificationPanelPrefab;
            }
            _currentPanel = _panelPool.GetObject();
            _currentPanel.transform.SetParent(transform, false);
        }

        private void CloseNotification(NotificationData notification)
        {
            EventNotificationClosed?.Invoke(this, new NotificationEventArgs(notification));
            _currentPanel.ClosePanel(); // Inicia o processo de fechamento com animação
            _currentPanel = null;
            if (_notificationQueue.Count == 0)
            {
                RestoreInitialFocus();
            }
        }

        public void OnPanelClosed(NotificationPanel panel)
        {
            if (panel == _currentPanel)
            {
                _panelPool.ReleaseObject(panel);
                _currentPanel = null;
                TryShowNextNotification();
            }
        }

        private System.Collections.IEnumerator AutoCloseNotification(NotificationPanel panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel == _currentPanel)
            {
                panel.ClosePanel(); // Fecha o painel automaticamente após o tempo definido
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

        private void OnEventNotificationAccepted(NotificationEventArgs e)
        {
            EventNotificationAccepted?.Invoke(this, e);
        }
    }

    public class NotificationEventArgs : EventArgs
    {
        public NotificationData NotificationData;

        public NotificationEventArgs(NotificationData notificationData)
        {
            NotificationData = notificationData;
        }
    }
}
