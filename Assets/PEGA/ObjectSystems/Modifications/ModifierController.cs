using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PEGA.ObjectSystems.Modifications
{
    public class ModifierController : MonoBehaviour
    {
        private readonly List<Modifier> _activeModifiers = new List<Modifier>();
        public bool HasActiveModifiers()
        {
            return _activeModifiers.Count > 0;
        }
        /// <summary>
        /// Adiciona um novo modificador.
        /// </summary>
        public void AddModifier(Modifier modifier)
        {
            _activeModifiers.Add(modifier);
        }

        /// <summary>
        /// Remove um modificador do tipo especificado.
        /// </summary>
        public void RemoveModifier(string type)
        {
            _activeModifiers.RemoveAll(mod => mod.Type == type);
        }

        /// <summary>
        /// Obtém o valor total de modificadores para um tipo específico.
        /// </summary>
        public float GetModifierValue(string type)
        {
            return _activeModifiers.Where(modifier => modifier.Type == type).Sum(modifier => modifier.Value);
        }

        /// <summary>
        /// Atualiza os modificadores ativos (removendo expirados).
        /// </summary>
        public void UpdateModifiers(float deltaTime)
        {
            for (var i = _activeModifiers.Count - 1; i >= 0; i--)
            {
                _activeModifiers[i].UpdateModifier(deltaTime);
                if (_activeModifiers[i].IsExpired())
                {
                    _activeModifiers.RemoveAt(i);
                }
            }
        }

    }
}