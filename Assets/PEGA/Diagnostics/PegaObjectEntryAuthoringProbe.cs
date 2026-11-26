using Immersive.Framework.ObjectEntry;
using Immersive.Framework.RuntimeContent;
using UnityEngine;

namespace PEGA.Diagnostics
{
    /// <summary>Diagnóstico para validar a autoria e a consulta de Object Entry no Inspector.</summary>
    [AddComponentMenu("PEGA/Diagnostics/Object Entry Authoring Probe")]
    public sealed class PegaObjectEntryAuthoringProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Referência identity-only para um ObjectEntryId indexado.")]
        private ObjectEntryIdReference identityOnly;

        [SerializeField, Tooltip("StableObjectReference com identidade e seletor de owner opcional.")]
        private StableObjectReference identityWithOptionalOwner;

        [SerializeField, Tooltip("Binding de consulta oficial no mesmo owner Route ou Activity.")]
        private ObjectEntryOccurrenceQueryBinding occurrenceQueryBinding;

        public ObjectEntryIdReference IdentityOnly => identityOnly;

        public StableObjectReference IdentityWithOptionalOwner => identityWithOptionalOwner;

#if UNITY_EDITOR
        [ContextMenu("Resolve Referenced Object Entry Occurrence")]
        private void ResolveReferencedOccurrence()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode after the Route or Activity owner commits to resolve this occurrence.", this);
                return;
            }

            if (occurrenceQueryBinding == null)
            {
                Debug.LogError("Assign the Framework Object Entry Occurrence Query binding.", this);
                return;
            }

            ObjectEntryOccurrenceResolution result = occurrenceQueryBinding.Resolve(identityWithOptionalOwner);
            string identity = identityWithOptionalOwner.TryGetObjectEntryId(out ObjectEntryId objectEntryId)
                ? objectEntryId.StableText
                : "<invalid>";

            if (result.Succeeded)
            {
                Debug.Log($"Object Entry '{identity}' resolved to '{result.Occurrence.name}' ({result.Status}).", result.Occurrence);
                return;
            }

            Debug.LogWarning($"Object Entry '{identity}' query returned {result.Status}: {result.Diagnostic}", this);
        }
#endif
    }
}
