using UnityEngine;
using ImmersiveGames.FaceSystem;

namespace ImmersiveGames.FaceSystem.Samples
{
    [RequireComponent(typeof(FaceController))]
    public sealed class FaceControllerTester : MonoBehaviour
    {
        [SerializeField]
        private FaceController controller;

        public FaceController Controller => controller;

        private void Reset()
        {
            controller = GetComponent<FaceController>();
        }

        private void OnValidate()
        {
            controller = GetComponent<FaceController>();
        }
    }

}
