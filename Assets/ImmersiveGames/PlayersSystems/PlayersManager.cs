using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.PlayerSystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.PlayersSystems
{
    public class PlayersManager : MonoBehaviour
    {
        [SerializeField] private List<Transform> spawnPoints;
        [SerializeField] private List<LayerMask> playerLayers;
        protected internal PlayerInputManager PlayerInputManager;

        #region Singleton

        public static PlayersManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DebugManager.LogWarning<PlayersManager>(
                    "Uma instância adicional de PlayersManager foi detectada e destruída.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            PlayerInputManager = GetComponent<PlayerInputManager>();
            Debug.Assert(PlayerInputManager != null,
                "PlayerInputManager não encontrado! Certifique-se de que o componente está anexado ao mesmo GameObject que o PlayersManager.");
            DebugManager.Log<PlayersManager>("PlayersManager inicializado e definido como persistente.");
        }

        #endregion

        #region Player Management

        private readonly List<PlayerMaster> _activePlayers = new List<PlayerMaster>();

        #endregion

        #region Unity Events

        private void OnEnable()
        {
            if (PlayerInputManager == null) return;
            PlayerInputManager.onPlayerJoined += HandlePlayerJoined;
            PlayerInputManager.onPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            if (PlayerInputManager == null) return;
            PlayerInputManager.onPlayerJoined -= HandlePlayerJoined;
            PlayerInputManager.onPlayerLeft -= HandlePlayerLeft;
        }

        #endregion

        #region Event Handlers

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            var playerMaster = playerInput.GetComponent<PlayerMaster>();
            if (playerMaster == null)
            {
                DebugManager.LogError<PlayersManager>(
                    $"[PlayersManager] PlayerMaster não encontrado para PlayerInput: {playerInput.playerIndex}");
                return;
            }

            if (!_activePlayers.Contains(playerMaster))
            {
                if (_activePlayers.Count >= spawnPoints.Count)
                {
                    DebugManager.LogError<PlayersManager>("Número insuficiente de pontos de spawn disponíveis.");
                    return;
                }

                if (_activePlayers.Count >= playerLayers.Count)
                {
                    DebugManager.LogError<PlayersManager>("Número insuficiente de layers disponíveis para os jogadores.");
                    return;
                }

                _activePlayers.Add(playerMaster);
                playerMaster.playerIndex = _activePlayers.Count - 1;
                DebugManager.Log<PlayersManager>(
                    $"Jogador conectado: {playerMaster.playerIndex}. Total de jogadores: {_activePlayers.Count}");

                PositionAndConfigurePlayer(playerInput, playerMaster);
            }
            else
            {
                DebugManager.LogWarning<PlayersManager>($"[PlayersManager] Jogador duplicado detectado: {playerMaster}");
            }
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            var playerMaster = playerInput.GetComponent<PlayerMaster>();
            if (playerMaster == null)
            {
                DebugManager.LogError<PlayersManager>(
                    $"[PlayersManager] PlayerMaster não encontrado para PlayerInput: {playerInput.playerIndex}");
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

                DebugManager.Log<PlayersManager>(
                    $"Jogador desconectado: {playerMaster}. Jogadores restantes: {_activePlayers.Count}");
            }
            else
            {
                DebugManager.LogWarning<PlayersManager>(
                    $"[PlayersManager] Tentativa de remover jogador inexistente: {playerMaster}");
            }
        }

        #endregion

        #region Private Methods

        private void PositionAndConfigurePlayer(PlayerInput playerInput, PlayerMaster playerMaster)
        {
            var playerParent = playerInput.transform.parent;
            playerInput.GetComponent<CharacterController>().enabled = false;
            playerParent.position = spawnPoints[_activePlayers.Count - 1].position;
            playerInput.GetComponent<CharacterController>().enabled = true;

            DebugManager.Log<PlayersManager>(
                $"Jogador Selecionado: {playerMaster.playerIndex}. Movido para: {playerParent.position}");

            var layerToAdd = (int)Mathf.Log(playerLayers[_activePlayers.Count - 1].value, 2);

            // Configura a câmera e layer
            playerParent.GetComponentInChildren<CinemachineVirtualCamera>().gameObject.layer = layerToAdd;
            playerParent.GetComponentInChildren<Camera>().cullingMask |= 1 << layerToAdd;
        }

        #endregion

        #region Public Methods

        public PlayerMaster GetPlayerMasterByIndex(int playerIndex)
        {
            foreach (var player in _activePlayers.Where(player => player.playerIndex == playerIndex))
            {
                DebugManager.Log<PlayersManager>($"Jogador encontrado: {player.playerIndex}");
                return player;
            }

            DebugManager.LogWarning<PlayersManager>($"Jogador com índice {playerIndex} não encontrado.");
            return null;
        }

        public void BroadcastMessageToAll(string message)
        {
            DebugManager.Log<PlayersManager>($"Enviando mensagem '{message}' para todos os jogadores ativos.");
            foreach (var player in _activePlayers)
            {
                player.SendMessage(message, SendMessageOptions.DontRequireReceiver);
            }
        }

        public void AddSpawnPoint(Transform startPoint)
        {
            if (spawnPoints.Contains(startPoint)) return;
            spawnPoints.Add(startPoint);
        }

        #endregion
    }
}
