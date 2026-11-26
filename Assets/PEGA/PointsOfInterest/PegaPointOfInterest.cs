using Immersive.Framework.ObjectEntry;
using UnityEngine;

namespace PEGA.PointsOfInterest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectEntryDeclaration))]
    [AddComponentMenu("PEGA/World/Point of Interest")]
    public sealed class PegaPointOfInterest : MonoBehaviour
    {
        public bool TryGetIdentity(out ObjectEntryId objectEntryId)
        {
            objectEntryId = default;
            return TryGetComponent(out ObjectEntryDeclaration declaration)
                && declaration.TryGetObjectEntryId(out objectEntryId);
        }
    }
}
