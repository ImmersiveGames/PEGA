using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.InputSystems;
using ImmersiveGames.PlayersSystems;
using PEGA.ObjectSystems.MovementSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using PEGA.ObjectSystems.MovementSystems.States;
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
            DebugManager.SetScriptDebugLevel<MovementContext>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<BaseState>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<JumpingState>(DebugManager.DebugLevels.All);
            DebugManager.SetScriptDebugLevel<GroundedState>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<JumpingDownState>(DebugManager.DebugLevels.All);
            DebugManager.SetScriptDebugLevel<IdleState>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<DashState>(DebugManager.DebugLevels.All);
            DebugManager.SetScriptDebugLevel<DeadState>(DebugManager.DebugLevels.None);
            DebugManager.SetScriptDebugLevel<WalkingState>(DebugManager.DebugLevels.None);
            
            //Drivers
            DebugManager.SetScriptDebugLevel<IInputDriver>(DebugManager.DebugLevels.None);
            
            //MovementController
            DebugManager.SetScriptDebugLevel<MovementController>(DebugManager.DebugLevels.None);
            
            
            
        }
    }
}