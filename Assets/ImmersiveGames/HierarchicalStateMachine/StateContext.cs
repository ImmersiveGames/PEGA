using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.MovementSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    [DefaultExecutionOrder(-10)]
    public class StateContext : MonoBehaviour,IStateContext
    {
        public event Action<StatesNames> OnStateEnter;
        public event Action<StatesNames> OnStateExit;
        
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

        public IHierarchicalState CurrentState { get; set; }

        #endregion
    }
}