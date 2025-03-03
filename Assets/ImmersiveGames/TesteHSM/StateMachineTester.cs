using UnityEngine;

namespace ImmersiveGames.TesteHSM
{
    public class StateMachineTester : MonoBehaviour
    {
        private HierarchicalStateMachine _stateMachine;

        private void Start()
        {
            _stateMachine = FindObjectOfType<HierarchicalStateMachine>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("🟢 [TESTE] Trocando de LeafA para LeafC dentro de MiddleA...");
                _stateMachine.TryTransition("SwitchToLeafC");
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                _stateMachine.TrySwitchBranch();
            }
        }
    }
}