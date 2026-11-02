using Immersive.Framework.ObjectEntry;
using Immersive.Framework.RuntimeContent;
using UnityEngine;

namespace PEGA.Diagnostics
{
    /// <summary>Componente temporário sem comportamento para validar os seletores de Object Entry no Inspector.</summary>
    [AddComponentMenu("PEGA/Diagnostics/Object Entry Authoring Probe")]
    public sealed class PEGAObjectEntryAuthoringProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Campo temporário para validar a seleção identity-only do ObjectEntryId.")]
        private ObjectEntryIdReference identityOnly;

        [SerializeField, Tooltip("Campo temporário para validar a seleção de identidade e o owner opcional independente.")]
        private StableObjectReference identityWithOptionalOwner;

        public ObjectEntryIdReference IdentityOnly => identityOnly;

        public StableObjectReference IdentityWithOptionalOwner => identityWithOptionalOwner;
    }
}
