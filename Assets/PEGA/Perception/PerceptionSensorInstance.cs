using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Instância de um sensor composta diretamente no Actor.</summary>
    [AddComponentMenu("PEGA/Perception/Sensor Instance")]
    public sealed class PerceptionSensorInstance : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Asset reutilizável que define avaliação, aquisição e cadência deste sensor. Adicione esta instância ao mesmo GameObject do Actor que contém a autoridade de Perception.")]
        private PerceptionSensorDefinition definition;

        public PerceptionSensorDefinition Definition => definition;

        public bool TryValidateConfiguration(out string diagnostic)
        {
            if (GetComponent<PerceptionSensorSet>() == null)
            {
                diagnostic = "Perception sensor instances must be components on the Actor root that owns the Perception Sensor Set.";
                return false;
            }

            if (definition == null)
            {
                diagnostic = "Perception sensor instance requires a definition asset.";
                return false;
            }

            return definition.TryValidateConfiguration(out diagnostic);
        }
    }
}
