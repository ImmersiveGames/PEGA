using System;
using UnityEngine;

namespace ImmersiveGames.NotificationsSystems
{
    [Serializable]
    public class NotificationData
    {
        public GameObject panelPrefab;
        public string message;
        public Action ConfirmAction;
    }
}