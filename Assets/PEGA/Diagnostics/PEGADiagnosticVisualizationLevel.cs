using UnityEngine;

namespace PEGA.Diagnostics
{
    /// <summary>Nível de informação mostrado pelos drawers de diagnóstico na Scene View.</summary>
    public enum PegaDiagnosticVisualizationLevel
    {
        [InspectorName("Off")]
        Off = 0,

        [InspectorName("Resumido")]
        Summary = 1,

        [InspectorName("Detalhado")]
        Detailed = 2
    }
}
