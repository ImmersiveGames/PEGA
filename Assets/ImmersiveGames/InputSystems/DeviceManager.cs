using System;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.NotificationsSystems;
using ImmersiveGames.Utils;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public class DeviceManager : Singleton<DeviceManager>
    {
        private NotificationManager _notificationManager;
        private string _message;
        
        public delegate void DeviceChangeHandler(InputDevice device, InputDeviceChange change);

        public event DeviceChangeHandler EventOnDeviceAdded;
        public event DeviceChangeHandler EventOnDeviceRemoved;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    _message = $"Device {device} was added";
                    DebugManager.Log<DeviceManager>(_message);
                    EventOnDeviceAdded?.Invoke(device, change);
                    break;
                case InputDeviceChange.Removed:
                    _message = $"Device {device} was removed";
                    DebugManager.Log<DeviceManager>(_message);
                    EventOnDeviceRemoved?.Invoke(device, change);
                    break;
                // Adicione outros casos conforme necessário
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.Disabled:
                case InputDeviceChange.UsageChanged:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.SoftReset:
                case InputDeviceChange.HardReset:
                default:
                    _message = $"An unknown error occurred with the {device}";
                    DebugManager.Log<DeviceManager>(_message);
                    throw new ArgumentOutOfRangeException(nameof(change), change, null);
            }
            var notificationData = new NotificationData
            {
                panelPrefab = _notificationManager.notificationPanelPrefab,
                message = _message
            };
            _notificationManager.AddNotification(notificationData);
        }
    }
}