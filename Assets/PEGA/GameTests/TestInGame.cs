using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using ImmersiveGames.PlayersSystems;

using UnityEngine;

namespace PEGA.GameTests
{
    public class TestInGame : MonoBehaviour
    {
        private void Awake()
        {
            //Aqui vão todos os Scripts que devem ter seus debugs ativos

            //PlayerManager
            DebugManager.SetScriptDebugLevel<PlayersManager>(DebugManager.DebugLevels.None);
            
            //Actions
            DebugManager.SetScriptDebugLevel<ActionManager>(DebugManager.DebugLevels.None);
            
        }
    }
}