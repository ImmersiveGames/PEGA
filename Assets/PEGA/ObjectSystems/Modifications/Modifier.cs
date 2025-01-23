using PEGA.ObjectSystems.Interfaces;

namespace PEGA.ObjectSystems.Modifications
{
    [System.Serializable]
    public class Modifier : IModifier
    {
        public string Type { get; private set; }
        public float Value { get; private set; }
        public float Duration { get; private set; }

        private float _remainingTime;

        public Modifier(string type, float value, float duration)
        {
            Type = type;
            Value = value;
            Duration = duration;
            _remainingTime = duration;
        }

        /// <summary>
        /// Atualiza o tempo restante do modificador.
        /// </summary>
        public void UpdateModifier(float deltaTime)
        {
            if (Duration > 0)
            {
                _remainingTime -= deltaTime;
            }
        }

        /// <summary>
        /// Verifica se o modificador expirou.
        /// </summary>
        public bool IsExpired()
        {
            return Duration > 0 && _remainingTime <= 0;
        }
    }
}