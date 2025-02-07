using System;
using System.Collections.Generic;
using PEGA.ObjectSystems.MovementSystems.States;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class HsmFactory
    {
        // 🔹 Alteramos para um dicionário de construtores (não instâncias prontas)
        private readonly Dictionary<StatesNames, Func<BaseState>> _stateConstructors = new();
        private readonly Dictionary<StatesNames, BaseState> _stateInstances = new();

        protected HsmFactory(StateContext currentContext)
        {
            // 🔹 Agora registramos estados dinamicamente
            RegisterState(StatesNames.Dead, () => new DeadState(currentContext, this));
        }

        // 🔹 Método para registrar um novo estado dinamicamente
        protected void RegisterState(StatesNames stateName, Func<BaseState> constructor)
        {
            _stateConstructors.TryAdd(stateName, constructor);
        }

        // 🔹 Método para criar um estado dinamicamente
        public BaseState CreateState(StatesNames stateName)
        {
            return _stateConstructors.TryGetValue(stateName, out var constructor) ? constructor() : null;
        }

        public BaseState GetState(StatesNames stateName)
        {
            if (_stateInstances.TryGetValue(stateName, out var state))
                return state; // 🔹 Sempre retorna a mesma instância do estado
            if (_stateConstructors.TryGetValue(stateName, out var constructor))
            {
                _stateInstances[stateName] = constructor(); // 🔹 Cria o estado apenas na primeira vez
            }
            else
            {
                throw new Exception($"Estado {stateName} não foi registrado na fábrica!");
            }

            return _stateInstances[stateName]; // 🔹 Sempre retorna a mesma instância do estado
        }
    }


    public enum StatesNames
    {
        Idle,
        Grounded,
        Jump,
        Walk,
        Fall,
        Dash,
        Dawn,
        Dead,
        InteractIdle
    }
}