using UnityEngine;

namespace PEGA.ObjectSystems.ObjectsScriptables
{
    /// <summary>
    /// A ideia aqui é que a skin possa configurar os módulos do player, mas eles não alteram o personagem
    /// apenas a forma que a skin opera o player cmo tamanho, tags e triggers.
    /// </summary>
    [CreateAssetMenu(fileName = "SkinSettings", menuName = "ImmersiveGames/PEGA/SkinsSettings", order = 202)]
    public class SkinSettings: ScriptableObject
    {
        [Header("Skin Animation Settings")]
        public string movementParameterName = "Movement"; // Nome do parâmetro do Blend Tree de movimento

        [Header("Skin CharacterController Settings")]
        public float height = 2.0f;
        public float radius = 0.5f;
        public Vector3 center = Vector3.zero;
    }
}