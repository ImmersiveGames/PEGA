using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PEGA.Diagnostics
{
    public sealed class PEGANavMeshTopologyProbeWindow : EditorWindow
    {
        private const string TargetScenePath = "Assets/PEGA/Scenes/MissionRoute.unity";
        private const int MaxDisplayedWalkableCells = 5000;

        [SerializeField] private float _sampleSpacing = 0.5f;
        [SerializeField] private bool _showRawSampling = true;
        [SerializeField] private bool _showTopologyOverlay = true;

        private PEGANavMeshTopologyAnalyzer.Snapshot _snapshot;
        private string _diagnostic;
        private float _analyzedSpacing;

        [MenuItem("PEGA/Diagnostics/NavMesh Topology Probe")]
        private static void Open()
        {
            GetWindow<PEGANavMeshTopologyProbeWindow>("NavMesh Topology");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneView;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneView;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PEGA · NavMesh Topology Probe", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Experimento de Editor para o armazém de um piso. Rasteriza diretamente a triangulação da NavMesh ativa e estima um grafo 2D; não altera cenas, NavMesh, NPCs ou GOAP. A cobertura bruta e a classificação topológica são exibidas separadamente.",
                MessageType.Info);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Cena ativa", string.IsNullOrEmpty(activeScene.path) ? activeScene.name : activeScene.path);
            if (!string.Equals(activeScene.path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox("Abra e torne ativa a cena MissionRoute para analisar o blockout do armazém.", MessageType.Warning);
            }

            _sampleSpacing = EditorGUILayout.Slider("Resolução da amostra (m)", _sampleSpacing, 0.25f, 2f);
            EditorGUILayout.HelpBox(
                "Uma célula é coberta quando intercepta área dos triângulos da NavMesh, mesmo que seu centro caia fora da superfície. O grafo ainda é uma aproximação 2D: células maiores podem fundir passagens próximas ou apagar detalhes.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analisar NavMesh")) AnalyzeCurrentScene();
                using (new EditorGUI.DisabledScope(_snapshot == null))
                {
                    if (GUILayout.Button("Limpar"))
                    {
                        _snapshot = null;
                        _diagnostic = null;
                        SceneView.RepaintAll();
                    }
                }
            }

            bool showRawSampling = EditorGUILayout.Toggle("Visualizar células amostradas", _showRawSampling);
            if (showRawSampling != _showRawSampling)
            {
                _showRawSampling = showRawSampling;
                SceneView.RepaintAll();
            }

            bool showTopologyOverlay = EditorGUILayout.Toggle("Visualizar classificação topológica", _showTopologyOverlay);
            if (showTopologyOverlay != _showTopologyOverlay)
            {
                _showTopologyOverlay = showTopologyOverlay;
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox(
                $"A Scene View limita os marcadores brutos a {MaxDisplayedWalkableCells:N0} posições para manter a visualização responsiva; a métrica de cobertura conta todas as células.",
                MessageType.None);

            if (!string.IsNullOrEmpty(_diagnostic))
            {
                EditorGUILayout.HelpBox(_diagnostic, MessageType.Warning);
            }

            if (_snapshot == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resultado da última análise", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Agente NavMesh", _snapshot.AgentTypeName);
            EditorGUILayout.LabelField("Resolução", $"{_analyzedSpacing:0.00} m");
            EditorGUILayout.LabelField("Triângulos de entrada", _snapshot.TriangleCount.ToString("N0"));
            EditorGUILayout.LabelField("Células da grade", _snapshot.GridCellCount.ToString("N0"));
            EditorGUILayout.LabelField("Células interceptadas pela NavMesh", _snapshot.WalkableSamples.ToString("N0"));
            float coverage = _snapshot.GridCellCount == 0
                ? 0f
                : (float)_snapshot.WalkableSamples / _snapshot.GridCellCount;
            EditorGUILayout.LabelField("Cobertura da grade", $"{coverage:P1}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Classificação topológica", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pontos do esqueleto", _snapshot.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Regiões de bifurcação", _snapshot.JunctionCenters.Count.ToString());
            EditorGUILayout.LabelField("Extremidades candidatas a sem saída", _snapshot.DeadEndEndpoints.ToString());
            EditorGUILayout.LabelField("Ciclos independentes", _snapshot.CycleRank.ToString());
            EditorGUILayout.LabelField("Trechos em ciclos", _snapshot.AlternativeEdgeCount.ToString());
            EditorGUILayout.LabelField("Componentes desconectados", _snapshot.ConnectedComponents.ToString());

            if (_snapshot.CycleRank > 0)
            {
                EditorGUILayout.HelpBox(
                    "Ciclos indicam caminhos alternativos no grafo amostrado. Confirme os trechos roxos contra o layout: resolução e afinamento podem criar ou remover conexões.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "A contagem de células mede apenas a interseção geométrica. Bifurcações, extremidades e ciclos são estimativas do esqueleto afinado; valide os traços na Scene View contra corredores e paredes reais.",
                MessageType.Info);

            DrawLegend();
        }

        private void AnalyzeCurrentScene()
        {
            _snapshot = null;
            _diagnostic = null;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
            {
                _diagnostic = "A análise está limitada a Assets/PEGA/Scenes/MissionRoute.unity.";
                return;
            }

            if (SceneManager.sceneCount != 1)
            {
                _diagnostic = "Feche as outras cenas abertas antes da análise: a API de triangulação agrega NavMeshes carregadas no Editor.";
                return;
            }

            NavMeshSurface surface = null;
            int surfaceCount = 0;
            foreach (GameObject root in activeScene.GetRootGameObjects())
            {
                NavMeshSurface[] found = root.GetComponentsInChildren<NavMeshSurface>(true);
                for (int index = 0; index < found.Length; index++)
                {
                    surface = found[index];
                    surfaceCount++;
                }
            }

            if (surfaceCount != 1 || surface == null || !surface.isActiveAndEnabled || surface.navMeshData == null)
            {
                _diagnostic = $"Esperava uma NavMeshSurface ativa com dados assados na cena; encontrei {surfaceCount}.";
                return;
            }

            if (!PEGANavMeshTopologyAnalyzer.TryAnalyze(surface, _sampleSpacing, out _snapshot, out _diagnostic))
            {
                return;
            }

            _analyzedSpacing = _sampleSpacing;
            SceneView.RepaintAll();
        }

        private void DrawSceneView(SceneView sceneView)
        {
            if (_snapshot == null || Event.current.type != EventType.Repaint) return;

            Color previousColor = Handles.color;
            if (_showRawSampling)
            {
                Handles.color = SamplingColor;
                int stride = Mathf.Max(1, Mathf.CeilToInt((float)_snapshot.WalkableCellPositions.Count / MaxDisplayedWalkableCells));
                float radius = Mathf.Clamp(_analyzedSpacing * 0.09f, 0.025f, 0.08f);
                for (int index = 0; index < _snapshot.WalkableCellPositions.Count; index += stride)
                {
                    Vector3 position = _snapshot.WalkableCellPositions[index] + Vector3.up * 0.025f;
                    Handles.DrawSolidDisc(position, Vector3.up, radius);
                }
            }

            if (!_showTopologyOverlay)
            {
                Handles.color = previousColor;
                return;
            }

            for (int index = 0; index < _snapshot.Edges.Count; index++)
            {
                PEGANavMeshTopologyAnalyzer.Edge edge = _snapshot.Edges[index];
                PEGANavMeshTopologyAnalyzer.Node from = _snapshot.Nodes[edge.From];
                PEGANavMeshTopologyAnalyzer.Node to = _snapshot.Nodes[edge.To];
                Handles.color = EdgeColor(edge, from, to);
                Handles.DrawAAPolyLine(3f, from.Position + Vector3.up * 0.06f, to.Position + Vector3.up * 0.06f);
            }

            Handles.color = DeadEndColor;
            for (int index = 0; index < _snapshot.Nodes.Count; index++)
            {
                if (!_snapshot.DeadEndNodes[index] || _snapshot.Adjacency[index].Count != 1) continue;
                Vector3 position = _snapshot.Nodes[index].Position + Vector3.up * 0.12f;
                Handles.DrawSolidDisc(position, Vector3.up, 0.13f);
                Handles.Label(position + Vector3.up * 0.16f, "Extremidade sem saída");
            }

            Handles.color = JunctionColor;
            for (int index = 0; index < _snapshot.JunctionCenters.Count; index++)
            {
                Vector3 position = _snapshot.JunctionCenters[index] + Vector3.up * 0.14f;
                Handles.DrawWireDisc(position, Vector3.up, 0.28f);
                Handles.Label(position + Vector3.up * 0.16f, "Bifurcação");
            }

            Handles.color = previousColor;
        }

        private static Color EdgeColor(
            PEGANavMeshTopologyAnalyzer.Edge edge,
            PEGANavMeshTopologyAnalyzer.Node from,
            PEGANavMeshTopologyAnalyzer.Node to)
        {
            if (edge.IsAlternative) return AlternativePathColor;
            if (from.IsDeadEnd || to.IsDeadEnd) return DeadEndColor;
            if (from.Degree >= 3 || to.Degree >= 3) return JunctionColor;
            return CorridorColor;
        }

        private static void DrawLegend()
        {
            EditorGUILayout.Space();
            DrawLegendItem(SamplingColor, "Célula que intercepta a NavMesh");
            DrawLegendItem(CorridorColor, "Corredor");
            DrawLegendItem(JunctionColor, "Bifurcação");
            DrawLegendItem(AlternativePathColor, "Caminho alternativo / ciclo");
            DrawLegendItem(DeadEndColor, "Ramo candidato sem saída");
        }

        private static void DrawLegendItem(Color color, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(18f, 14f, GUILayout.Width(18f));
                EditorGUI.DrawRect(swatch, color);
                GUILayout.Label(label);
            }
        }

        private static readonly Color CorridorColor = new Color(0.1f, 0.85f, 1f, 0.95f);
        private static readonly Color SamplingColor = new Color(1f, 0.92f, 0.12f, 0.8f);
        private static readonly Color JunctionColor = new Color(1f, 0.62f, 0.05f, 1f);
        private static readonly Color AlternativePathColor = new Color(0.85f, 0.2f, 1f, 1f);
        private static readonly Color DeadEndColor = new Color(1f, 0.2f, 0.18f, 1f);
    }
}
