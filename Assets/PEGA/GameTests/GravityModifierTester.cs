using PEGA.ObjectSystems.Modifications;
using UnityEngine;

namespace PEGA.GameTests
{
    public class GravityModifierTester : MonoBehaviour
    {
        private ModifierController _modifierController;

        private void Awake()
        {
            // Obtém o ModifierController no objeto
            _modifierController = GetComponent<ModifierController>();

            if (_modifierController == null)
            {
                Debug.LogError("ModifierController não encontrado no objeto! Adicione um componente ModifierController.");
            }
        }

        private void Update()
        {
            // Tecla para aplicar um buff de gravidade (diminui a gravidade)
            if (Input.GetKeyDown(KeyCode.O)) // 'O' para Buff
            {
                ApplyBuff();
            }

            // Tecla para aplicar um debuff de gravidade (aumenta a gravidade)
            if (Input.GetKeyDown(KeyCode.K)) // 'k' para Debuff
            {
                ApplyDebuff();
            }

            // Tecla para resetar os modificadores
            if (Input.GetKeyDown(KeyCode.M)) // 'M' para Reset
            {
                ResetModifiers();
            }
            // Tecla para resetar os modificadores
            if (Input.GetKeyDown(KeyCode.P)) // 'M' para Reset
            {
                ApplyJumpBust();
            }
        }

        private void ApplyJumpBust()
        {
            _modifierController.AddModifier(new Modifier("JumpBoost", 10.0f,5f)); // Reduz a gravidade
            Debug.Log("Adiciona um Boost no Pulo de : 10.0");
        }
        private void ApplyBuff()
        {
            _modifierController.AddModifier(new Modifier("GravityReduction", -2.0f,5f)); // Reduz a gravidade
            Debug.Log("Buff de gravidade aplicado: -2.0");
        }

        private void ApplyDebuff()
        {
            _modifierController.AddModifier(new Modifier("GravityReduction", 3.0f, 5f)); // Aumenta a gravidade
            Debug.Log("Debuff de gravidade aplicado: +3.0");
        }

        private void ResetModifiers()
        {
            _modifierController.RemoveModifier("GravityReduction"); // Remove todos os modificadores de gravidade
            Debug.Log("Modificadores de gravidade resetados.");
        }
    }
}