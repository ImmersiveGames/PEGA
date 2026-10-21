using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Escape Destination")]
    public sealed class PEGANpcEscapeDestination : MonoBehaviour
    {
        public Vector3 WorldPosition => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.95f, 0.35f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
            Gizmos.color = previousColor;
        }
#endif
    }
}
