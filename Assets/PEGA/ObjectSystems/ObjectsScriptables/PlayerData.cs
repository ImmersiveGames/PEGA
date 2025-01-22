using UnityEngine;

namespace PEGA.ObjectSystems.ObjectsScriptables
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "ImmersiveGames/PEGA/Player", order = 101)]
    public class PlayerData : ObjectDataScriptable
    {
        public SkinCollections skinPurchaseCollection;
    }
}