using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.InputSystems;
using ImmersiveGames.PlayersSystems;
using PEGA.ObjectSystems.MovementSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.GameTests
{
    public class TestInGame : MonoBehaviour
    {
        private void Awake()
        {
            //Aqui vão todos os Roteiros que devem ter seus debugs ativos

            //PlayerManager
            DebugManager.SetScriptDebugLevel<PlayersManager>(DebugManager.DebugLevels.None);
            
            //Actions
            DebugManager.SetScriptDebugLevel<ActionManager>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<ActionMapManager>(DebugManager.DebugLevels.None);
            
            //States
            DebugManager.SetScriptDebugLevel<BaseState>(DebugManager.DebugLevels.All);
            
            //Drivers
            DebugManager.SetScriptDebugLevel<IMovementDriver>(DebugManager.DebugLevels.None);
            
            //MovementController
            DebugManager.SetScriptDebugLevel<MovementController>(DebugManager.DebugLevels.None);
            
            
            
        }
    }
}