using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
    public abstract class StateModule : MonoBehaviour
    {
        [SerializeField] protected int priority;
        [SerializeField] protected Blackboard blackboard;
        protected StateManager StateManager;

        protected virtual void Awake()
        {
            StateManager = GetComponent<StateManager>();
        }

        public int Priority => priority;
        public abstract void Enter();
        public abstract void Exit();
        public abstract void UpdateState();
        public abstract bool CanBeInterrupted();
    }
}