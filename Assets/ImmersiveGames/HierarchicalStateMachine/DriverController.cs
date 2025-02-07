using System;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class DriverController : MonoBehaviour
    {
        protected StateContext Context;

        protected virtual void Awake()
        {
            SwitchToPlayerControl();
        }

        protected virtual void Update()
        {
            Context.ActualDriver?.UpdateDriver(); // Atualiza estados de input.
        }
        private void SetInputSource(IMovementDriver actualDriver)
        {
            if (Context.ActualDriver == actualDriver) return;
            
            Context.ActualDriver?.ExitDriver();
            Context.ActualDriver = actualDriver;
            Context.ActualDriver.InitializeDriver();
        }
        private void SwitchToPlayerControl()
        {
            SetInputSource(new PlayerMovementDriver(GetComponent<PlayerInput>()));
        }

        public void SwitchToAIControl()
        {
            SetInputSource(new NullMovementDriver(transform));
        }

        private void ResetHistoryDriver()
        {
            Context.ActualDriver?.Reset();
        }
   
    }
}