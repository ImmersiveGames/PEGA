using System.Collections.Generic;
using System.Linq;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.PlayerSystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.GamePlaySystems
{
    public class PlayerManager : MonoBehaviour
    {
        #region Singleton

        public static PlayerManager Instance { get; private set; }
        private PlayerInputManager _playerInputManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DebugManager.LogWarning<PlayerManager>("Uma instância adicional de PlayerManager foi detectada e destruída.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _playerInputManager = GetComponent<PlayerInputManager>();
            Debug.Assert(_playerInputManager != null, "PlayerInputManager não encontrado! Certifique-se de que o componente está anexado ao mesmo GameObject que o PlayerManager.");
            DebugManager.Log<PlayerManager>("PlayerManager inicializado e definido como persistente.");
        }

        #endregion

        #region Player Management

        private readonly List<PlayerMaster> _activePlayers = new List<PlayerMaster>();

        #endregion

        #region Unity Events

        private void OnEnable()
        {
            if (_playerInputManager == null) return;
            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            if (_playerInputManager == null) return;
            _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            _playerInputManager.onPlayerLeft -= HandlePlayerLeft;
        }

        #endregion

        #region Event Handlers

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            var playerMaster = playerInput.GetComponent<PlayerMaster>();
            if (playerMaster == null)
            {
                DebugManager.LogError<PlayerManager>($"[PlayerManager] PlayerMaster não encontrado para PlayerInput: {playerInput.playerIndex}");
                return;
            }

            if (!_activePlayers.Contains(playerMaster))
            {
                _activePlayers.Add(playerMaster);
                playerMaster.playerIndex = _activePlayers.Count - 1;
                DebugManager.Log<PlayerManager>($"Jogador conectado: {playerMaster.playerIndex}. Total de jogadores: {_activePlayers.Count}");
            }
            else
            {
                DebugManager.LogWarning<PlayerManager>($"[PlayerManager] Jogador duplicado detectado: {playerMaster}");
            }
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            var playerMaster = playerInput.GetComponent<PlayerMaster>();
            if (playerMaster == null)
            {
                DebugManager.LogError<PlayerManager>($"[PlayerManager] PlayerMaster não encontrado para PlayerInput: {playerInput.playerIndex}");
                return;
            }

            if (_activePlayers.Contains(playerMaster))
            {
                _activePlayers.Remove(playerMaster);

                // Atualiza os índices dos jogadores restantes
                for (var i = 0; i < _activePlayers.Count; i++)
                {
                    _activePlayers[i].playerIndex = i;
                }

                DebugManager.Log<PlayerManager>($"Jogador desconectado: {playerMaster}. Jogadores restantes: {_activePlayers.Count}");
            }
            else
            {
                DebugManager.LogWarning<PlayerManager>($"[PlayerManager] Tentativa de remover jogador inexistente: {playerMaster}");
            }
        }

        #endregion

        #region Public Methods

        public PlayerMaster GetPlayerMasterByIndex(int playerIndex)
        {
            foreach (var player in _activePlayers.Where(player => player.playerIndex == playerIndex))
            {
                DebugManager.Log<PlayerManager>($"Jogador encontrado: {player.playerIndex}");
                return player;
            }

            DebugManager.LogWarning<PlayerManager>($"Jogador com índice {playerIndex} não encontrado.");
            return null;
        }

        public void BroadcastMessageToAll(string message)
        {
            DebugManager.Log<PlayerManager>($"Enviando mensagem '{message}' para todos os jogadores ativos.");
            foreach (var player in _activePlayers)
            {
                player.SendMessage(message, SendMessageOptions.DontRequireReceiver);
            }
        }

        #endregion
    }
}
