using System;
using ImmersiveGames.PlayersSystems;
using PEGA.GamePlaySystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.CameraSystems
{
    public class CameraMaster : MonoBehaviour
    {
        private PlayerInputManager _playerInputManager;

        private void Awake()
        {
            _playerInputManager = PlayersManager.Instance.PlayerInputManager;
        }

        private void OnEnable()
        {
            _playerInputManager.onPlayerJoined += DisableThis;
        }

        private void OnDisable()
        {
            _playerInputManager.onPlayerJoined -= DisableThis;
        }

        private void DisableThis(PlayerInput obj)
        {
            this.gameObject.SetActive(false);
        }
    }
}