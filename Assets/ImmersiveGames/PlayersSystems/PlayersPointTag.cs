using System;
using UnityEngine;

namespace ImmersiveGames.PlayersSystems
{
    public class PlayersPointTag: MonoBehaviour
    {
        
        private void Awake()
        {
            if (PlayersManager.Instance)
            {
                PlayersManager.Instance.AddSpawnPoint(transform);
            }
        }
    }
}