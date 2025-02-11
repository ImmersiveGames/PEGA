using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.MovementSystems;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    [DefaultExecutionOrder(-10)]
    public class StateContext : MonoBehaviour,IStateContext
    {
        public event Action<StatesNames> OnGlobalStateEnter;
        public event Action<StatesNames> OnGlobalStateExit;
        
        #region Call Events

        public void GlobalNotifyStateEnter(StatesNames newState)
        {
            DebugManager.Log<MovementContext>($"Chamou o Evento paa o Estado : {newState}");
            OnGlobalStateEnter?.Invoke(newState);
        }
        public void GlobalNotifyStateExit(StatesNames newState)
        {
            OnGlobalStateExit?.Invoke(newState);
        }

        public BaseState CurrentState { get; set; }

        #endregion
    }
}