using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.MovementSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    [DefaultExecutionOrder(-10)]
    public class StateContext : MonoBehaviour
    {
        public event Action<StatesNames> OnStateEnter;
        public event Action<StatesNames> OnStateExit;
        
        internal BaseState CurrentState;
        internal IMovementDriver ActualDriver;
        
        #region Call Events

        public void GlobalNotifyStateEnter(StatesNames newState)
        {
            DebugManager.Log<MovementContext>($"Chamou o Evento paa o Estado : {newState}");
            OnStateEnter?.Invoke(newState);
        }
        public void GlobalNotifyStateExit(StatesNames newState)
        {
            OnStateExit?.Invoke(newState);
        }

        #endregion
    }
}