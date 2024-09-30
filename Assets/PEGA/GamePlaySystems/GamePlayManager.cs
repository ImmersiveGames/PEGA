using ImmersiveGames.DebugSystems;
using UnityEngine;

namespace PEGA.GamePlaySystems
{
    public class GamePlayManager : MonoBehaviour
    {
        public static GamePlayManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DebugManager.Log<GamePlayManager>("GamePlayManager instanciado.");
            }
            else
            {
                Destroy(gameObject);
                DebugManager.LogWarning<GamePlayManager>("Tentativa de criar uma segunda instância de SteamStatsService foi evitada.");
            }
        }
    }
}