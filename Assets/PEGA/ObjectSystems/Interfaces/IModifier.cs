namespace PEGA.ObjectSystems.Interfaces
{
    public interface IModifier
    {
        string Type { get; } // Tipo do modificador (ex.: "Height", "Speed", "Gravity").
        float Value { get; } // Valor que o modificador aplica.
        float Duration { get; } // Duração do modificador em segundos (0 = permanente).
    }
}