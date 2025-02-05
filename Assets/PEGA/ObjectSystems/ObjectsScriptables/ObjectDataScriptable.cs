
using UnityEngine;
using UnityEngine.Localization;

namespace PEGA.ObjectSystems.ObjectsScriptables
{
    public abstract class ObjectDataScriptable : ScriptableObject
    {
        [Header("Default Settings")]
        public new string name;
        public LocalizedString localizeName;
        
        [Header("Movement Base Settings")]
        //public string movementParameterName = "Movement"; // Nome do parâmetro do Blend Tree de movimento
        public float speed = 5f; // Velocidade do jogador
        public float rotationSpeed = 10f; // Velocidade de rotação do jogador
        public float gravityModifier = 2f; // Modificador da gravidade
        public float jumpHeight = 2.0f; // Altura máxima do pulo
        public float jumpSpeed = 5.0f; // Velocidade do pulo

        [Header("Dash Base Settings")]
        public float dashSpeed = 20f; // Velocidade do dash
        public float dashDuration = 0.2f; // Duração do dash
        public float dashCooldown = 1f; // Tempo de recarga do dash
        public bool canDash = true; // Controle para verificar se pode dar dash
        
        [Header("Skins Settings")]
        public SkinCollections skinDefaultCollection;

        public string GetName()
        {
            return localizeName.IsEmpty ? name : localizeName.GetLocalizedString();
        }
    }
    
}