using System.Collections.Generic;
using PEGA.ObjectSystems.Tags;
using UnityEngine;

namespace PEGA.ObjectSystems.ObjectsScriptables
{
    [CreateAssetMenu(fileName = "SkinCollections", menuName = "ImmersiveGames/PEGA/SkinsCollection", order = 201)]
    public class SkinCollections: ScriptableObject
    {
        [Header("Skin Settings")]
        public List<SkinAttach> skinAttaches;

        public GameObject GetObjectSkin(int forceIndex = -1, bool randomSkin = false)
        {
            // Retorna o índice forçado se for válido
            if (forceIndex >= 0 && forceIndex < skinAttaches.Count)
            {
                return skinAttaches[forceIndex].gameObject;
            }

            // Determina o índice de forma aleatória se solicitado e possível
            var indexSkin = randomSkin && skinAttaches.Count > 0 
                ? Random.Range(0, skinAttaches.Count) 
                : 0;

            return skinAttaches[indexSkin].gameObject;
        }

    }
}