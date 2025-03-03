using UnityEngine;
using System.Linq;

namespace ImmersiveGames.TesteHSM
{
    public class HierarchicalStateMachine : MonoBehaviour
    {
        private StateMachine _stateMachine;
        private StateWithChildren _root;
        private StateWithChildren _middleA, _middleB;
        private State _leafA, _leafB, _leafC;

        private void Start()
        {
            _stateMachine = new StateMachine();

            // Criando Estados
            _root = new StateWithChildren("Root", _stateMachine);
            _middleA = new StateWithChildren("MiddleA", _stateMachine, _root);
            _middleB = new StateWithChildren("MiddleB", _stateMachine, _root);
            _leafA = new StateWithChildren("LeafA", _stateMachine, _middleA);
            _leafB = new StateWithChildren("LeafB", _stateMachine, _middleB);
            _leafC = new StateWithChildren("LeafC", _stateMachine, _middleA);

            // Configurando Hierarquia
            _root.AddChild(_middleA);
            _root.AddChild(_middleB);
            _middleA.AddChild(_leafA);
            _middleA.AddChild(_leafC);
            _middleB.AddChild(_leafB);

            // Definir Transições Dinâmicas
            _middleA.AddTransition("SwitchToLeafC", _leafC);
            _root.AddTransition("SwitchBranch", _middleB);

            // Definir Estado Inicial
            _stateMachine.SetState(_root);
            _root.SetActiveChild("MiddleA");
            _middleA.SetActiveChild("LeafA");
        }

        private void Update()
        {
            _stateMachine.Update();
        }

        public bool TryTransition(string trigger)
        {
            return _stateMachine.TryTransition(trigger);
        }
        public void TrySwitchBranch()
        {
            Debug.Log("🔴 [TESTE] Trocando de galho em Root...");

            // Se houver um estado ativo dentro de Root, primeiro saia dele
            if (_root.ActiveChild is StateWithChildren activeMiddle)
            {
                if (activeMiddle.ActiveChild != null)
                {
                    //OnExitState?.Invoke(activeMiddle.ActiveChild.StateName);
                    activeMiddle.ActiveChild.Exit();
                }

                //OnExitState?.Invoke(activeMiddle.StateName);
                activeMiddle.Exit();
            }

            _stateMachine.TryTransition("SwitchBranch");
        }


        public T GetState<T>(string stateName) where T : State
        {
            return _stateMachine.GetState<T>(stateName);
        }
    }
}