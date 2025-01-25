using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PEGA.ObjectSystems.ObjectsScriptables
{
    [CreateAssetMenu(fileName = "AttributesBaseData", menuName = "ImmersiveGames/PEGA/Attributes", order = 101)]
    public class AttributesBaseData : ScriptableObject
    {
        [MinMax(0.0f,5.0f)]
        public float attBase;
        [MinMax(0.0f,5.0f)]
        public float attStrength;
        [MinMax(0.0f,5.0f)]
        public float attAgility;
        [MinMax(0.0f,5.0f)]
        public float attPresence;
        [MinMax(0.0f,5.0f)]
        public float attDefense;
        [MinMax(0.0f,5.0f)]
        public float attSkill;
        
    }
}