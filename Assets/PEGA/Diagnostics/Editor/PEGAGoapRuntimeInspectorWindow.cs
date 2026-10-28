using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.NPC;
using UnityEditor;
using UnityEngine;

namespace PEGA.Diagnostics.Editor
{
    /// <summary>Editor-only, read-only view of the GOAP snapshots retained by a selected NPC.</summary>
    public sealed class PEGAGoapRuntimeInspectorWindow : EditorWindow
    {
        private const double SampleIntervalSeconds = 0.25d;
        private const int MaximumHistoryEntries = 40;
        private static readonly string[] TabNames =
        {
            "Geral", "Desejos", "Plano", "Conhecimento", "Histórico"
        };

        private readonly List<string> _history = new List<string>();
        private PEGANpcAutonomy _observedAutonomy;
        private double _nextSampleAt;
        private string _previousActivePlan;
        private string _previousSelection;
        private readonly Dictionary<string, string> _previousExecutions = new Dictionary<string, string>(StringComparer.Ordinal);
        private Vector2 _scrollPosition;
        private int _selectedTab;
        private bool _showOverviewDetails;
        private bool _showOverviewSelectionDetails;
        private bool _showDesireDetails;
        private bool _showPlanDetails;
        private bool _showCausalDetails;
        private bool _showActionDetails;
        private bool _showKnowledgeMetadata;
        private bool _showGangTargetDetails;

        [MenuItem("PEGA/Diagnostics/GOAP Runtime Inspector")]
        private static void OpenWindow()
        {
            GetWindow<PEGAGoapRuntimeInspectorWindow>("GOAP Runtime Inspector");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnSelectionChanged()
        {
            PEGANpcAutonomy selected = ResolveSelectedAutonomy();
            if (selected == _observedAutonomy)
            {
                Repaint();
                return;
            }

            _observedAutonomy = selected;
            ResetObservation();
            _scrollPosition = Vector2.zero;
            _selectedTab = 0;
            Repaint();
        }

        private void OnEditorUpdate()
        {
            PEGANpcAutonomy selected = ResolveSelectedAutonomy();
            if (selected != _observedAutonomy)
            {
                _observedAutonomy = selected;
                ResetObservation();
                _scrollPosition = Vector2.zero;
                _selectedTab = 0;
            }

            if (!EditorApplication.isPlaying || selected == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextSampleAt)
            {
                return;
            }

            _nextSampleAt = now + SampleIntervalSeconds;
            Observe(selected);
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ResetObservation();
            _observedAutonomy = ResolveSelectedAutonomy();
            _scrollPosition = Vector2.zero;
            _selectedTab = 0;
            Repaint();
        }

        private void OnGUI()
        {
            PEGANpcAutonomy autonomy = ResolveSelectedAutonomy();
            DrawSelection(autonomy);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Entre no Play Mode para observar snapshots reais do GOAP. A janela não inicia nem altera a simulação.",
                    MessageType.Info);
                return;
            }

            if (autonomy == null)
            {
                EditorGUILayout.HelpBox(
                    "Selecione no Hierarchy um NPC com PEGANpcAutonomy, ou qualquer um de seus objetos filhos.",
                    MessageType.Info);
                return;
            }

            RuntimeView view = PEGAGoapRuntimeInspectorReader.Read(autonomy);
            int selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
            if (selectedTab != _selectedTab)
            {
                _selectedTab = selectedTab;
                _scrollPosition = Vector2.zero;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawReadStatus(view);
            switch (_selectedTab)
            {
                case 0:
                    DrawOverview(view);
                    break;
                case 1:
                    DrawDesires(view);
                    break;
                case 2:
                    DrawPlanning(view);
                    DrawActions(view);
                    break;
                case 3:
                    DrawKnowledge(view);
                    DrawGangKnowledge(view);
                    break;
                case 4:
                    DrawHistory();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSelection(PEGANpcAutonomy autonomy)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GOAP Runtime Inspector", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(EditorApplication.isPlaying ? "PLAY MODE" : "EDIT MODE", EditorStyles.miniBoldLabel, GUILayout.Width(75f));
            EditorGUILayout.EndHorizontal();

            GameObject selected = Selection.activeGameObject;
            EditorGUILayout.LabelField("NPC observado", autonomy != null ? autonomy.gameObject.name : "<não encontrado>", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Objeto selecionado no Hierarchy", selected != null ? selected.name : "<nenhum>", EditorStyles.miniLabel);
            if (autonomy != null && !autonomy.isActiveAndEnabled)
            {
                EditorGUILayout.HelpBox(
                    "O PEGANpcAutonomy selecionado está desativado. Os valores exibidos são apenas os snapshots que ainda estiverem retidos.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawReadStatus(RuntimeView view)
        {
            if (view.Issues.Count == 0)
            {
                return;
            }

            for (int i = 0; i < view.Issues.Count; i++)
            {
                EditorGUILayout.HelpBox(view.Issues[i], MessageType.Warning);
            }
        }

        private void DrawOverview(RuntimeView view)
        {
            EditorGUILayout.LabelField("Visão geral · agora", EditorStyles.largeLabel);
            if (view.HasActivePlan)
            {
                PEGANpcPlanSnapshot plan = view.ActivePlan;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Desejo selecionado no plano ativo", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(plan.DesireName) ? "Nome não exposto" : plan.DesireName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Goal configurado", FindConfiguredGoalName(view.Profile, plan.DesireId, view.LatestSelection));
                EditorGUILayout.LabelField("Verdade atual do Goal", "Não exposta pelo runtime", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Action ativa", FormatActionName(plan.ActionId));
                EditorGUILayout.LabelField("Target / posição conhecida", plan.TargetId.IsValid ? $"{FormatTargetKind(plan.TargetKind)} · {plan.TargetPosition:F2}" : "<Target indisponível>");

                IPEGANpcActionProvider provider = FindProvider(view.ActionProviders, plan.ActionId);
                if (provider == null)
                {
                    EditorGUILayout.HelpBox("O snapshot contém um plano ativo, mas seu provider de execução não está disponível para leitura.", MessageType.Warning);
                }
                else
                {
                    PEGANpcActionExecutionSnapshot execution = provider.CurrentExecution;
                    DrawExecutionState(provider.Outcome, execution.Outcome, provider.Contract.ActionId);
                }

                _showOverviewDetails = EditorGUILayout.Foldout(_showOverviewDetails, "Detalhes do snapshot ativo", true);
                if (_showOverviewDetails)
                {
                    DrawPlanDetails(plan);
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("O snapshot atual do Autonomy não contém um plano ativo.", MessageType.Info);
                DrawRunningProvidersWithoutPlan(view.ActionProviders);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Último resultado do planejador", EditorStyles.boldLabel);
            if (view.LatestSelection == null)
            {
                EditorGUILayout.LabelField("Nenhum resultado armazenado.");
                return;
            }

            PEGANpcPlanSelectionResult selection = view.LatestSelection;
            EditorGUILayout.HelpBox(
                selection.HasSelectedPlan
                    ? $"Avaliação armazenada: selecionou {selection.SelectedPlan.DesireName} / {FormatActionName(selection.SelectedPlan.ActionId)}. Isso não confirma que a Action continua ativa agora."
                    : $"Avaliação armazenada sem plano selecionado (válida={selection.IsValid}). Isso não determina por si só o estado atual do NPC.",
                selection.HasSelectedPlan ? MessageType.None : MessageType.Info);
            _showOverviewSelectionDetails = EditorGUILayout.Foldout(_showOverviewSelectionDetails, "Contexto e diagnóstico da avaliação", true);
            if (_showOverviewSelectionDetails)
            {
                EditorGUILayout.LabelField("Contexto / modo", $"{selection.Context} / {selection.ValidationMode}");
                EditorGUILayout.LabelField("Plano selecionado", selection.HasSelectedPlan ? "sim" : "não");
                if (!string.IsNullOrWhiteSpace(selection.Diagnostic))
                {
                    EditorGUILayout.LabelField("Diagnóstico", selection.Diagnostic, EditorStyles.wordWrappedLabel);
                }

                if (selection.HasSelectedPlan)
                {
                    EditorGUILayout.LabelField("Target da seleção", selection.SelectedPlan.TargetId.ToString());
                }
            }
        }

        private static void DrawExecutionState(
            PEGANpcActionOutcome providerOutcome,
            PEGANpcActionOutcome snapshotOutcome,
            PEGANpcPlanningActionId actionId)
        {
            MessageType messageType = providerOutcome == PEGANpcActionOutcome.Invalid ||
                                     providerOutcome == PEGANpcActionOutcome.Interrupted
                ? MessageType.Warning
                : providerOutcome == PEGANpcActionOutcome.Running
                    ? MessageType.Info
                    : MessageType.None;
            EditorGUILayout.HelpBox(
                $"Execução observada · {FormatActionName(actionId)}: {FormatOutcome(providerOutcome)} · snapshot: {FormatOutcome(snapshotOutcome)}",
                messageType);
        }

        private static void DrawRunningProvidersWithoutPlan(IReadOnlyList<IPEGANpcActionProvider> providers)
        {
            bool foundRunningProvider = false;
            for (int i = 0; i < providers.Count; i++)
            {
                IPEGANpcActionProvider provider = providers[i];
                if (provider.Outcome == PEGANpcActionOutcome.Running)
                {
                    foundRunningProvider = true;
                    EditorGUILayout.HelpBox(
                        $"Provider {FormatActionName(provider.Contract.ActionId)} reporta Em execução, mas o Autonomy não expõe plano ativo neste snapshot.",
                        MessageType.Warning);
                }
            }

            if (!foundRunningProvider)
            {
                EditorGUILayout.LabelField("Nenhum provider reporta uma Action em execução.", EditorStyles.miniLabel);
            }
        }

        private void DrawDesires(RuntimeView view)
        {
            EditorGUILayout.LabelField("Desejos configurados", EditorStyles.largeLabel);
            if (view.Profile == null)
            {
                EditorGUILayout.HelpBox("Desire Profile indisponível no componente selecionado.", MessageType.Info);
                return;
            }

            PEGANpcDesireContext context = view.LatestSelection != null
                ? view.LatestSelection.Context
                : PEGANpcDesireContext.Normal;
            EditorGUILayout.LabelField("Profile / contexto", $"{view.Profile.name} / {context}", EditorStyles.miniLabel);
            if (!view.Profile.TryGetContext(context, out PEGANpcDesireContextDefinition definition, out string diagnostic))
            {
                EditorGUILayout.HelpBox($"Configuração de Desejos indisponível: {diagnostic}", MessageType.Warning);
                return;
            }

            PEGANpcDesireStateSnapshot satisfaction = view.DesireState;
            IReadOnlyList<PEGANpcDesirePlanEvaluation> evaluations = view.LatestSelection != null
                ? view.LatestSelection.DesireEvaluations
                : Array.Empty<PEGANpcDesirePlanEvaluation>();
            EditorGUILayout.HelpBox(
                "Aplicabilidade e peso efetivo vêm da última avaliação armazenada. A verdade lógica atual do Goal não é exposta pelo runtime.",
                MessageType.None);
            _showDesireDetails = EditorGUILayout.ToggleLeft("Exibir IDs, configuração e diagnósticos", _showDesireDetails);

            IReadOnlyList<PEGANpcDesireConfiguration> desires = definition.Desires;
            if (desires.Count == 0)
            {
                EditorGUILayout.LabelField("Nenhum Desejo configurado para este contexto.");
                return;
            }

            for (int i = 0; i < desires.Count; i++)
            {
                PEGANpcDesireConfiguration configuration = desires[i];
                if (configuration == null || configuration.Definition == null)
                {
                    EditorGUILayout.HelpBox($"Desire Setting {i}: definição indisponível.", MessageType.Warning);
                    continue;
                }

                PEGANpcDesireDefinition desire = configuration.Definition;
                PEGANpcDesireSatisfactionSnapshot current = default;
                bool hasSatisfaction = satisfaction != null && satisfaction.TryGet(desire.DesireId, out current);
                PEGANpcDesirePlanEvaluation evaluation = FindDesireEvaluation(evaluations, desire.DesireId);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(desire.DisplayName) ? desire.DesireId : desire.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Goal configurado", desire.DesiredGoalCondition != null ? desire.DesiredGoalCondition.name : "<condição indisponível>");
                EditorGUILayout.LabelField("Peso base", configuration.BaseWeight.ToString("0.###"));
                EditorGUILayout.LabelField("Satisfação runtime", hasSatisfaction ? current.Satisfaction.ToString("0.###") : "<sem snapshot disponível>");
                EditorGUILayout.LabelField("Conclusões confirmadas", hasSatisfaction ? current.CompletedExecutions.ToString() : "<sem snapshot disponível>");
                EditorGUILayout.LabelField("Aplicável na última avaliação", evaluation != null ? evaluation.IsApplicable.ToString() : "<sem avaliação armazenada>");
                EditorGUILayout.LabelField("Peso efetivo na última avaliação", evaluation != null ? evaluation.EffectiveWeight.ToString("0.###") : "<não exposto>");
                if (_showDesireDetails)
                {
                    EditorGUILayout.LabelField("ID", string.IsNullOrWhiteSpace(desire.DesireId) ? "<indisponível>" : desire.DesireId);
                    EditorGUILayout.LabelField("Habilitado no contexto", configuration.EnabledForContext.ToString());
                    EditorGUILayout.LabelField("Limite de conclusões", configuration.MaximumCompletedExecutions < 0 ? "sem limite" : configuration.MaximumCompletedExecutions.ToString());
                    EditorGUILayout.LabelField("Ganho / máximo de satisfação", $"{configuration.SatisfactionGain:0.###} / {configuration.MaximumSatisfaction:0.###}");
                    EditorGUILayout.LabelField("Redução de peso por satisfação", configuration.SatisfactionWeightModifier.ToString("0.###"));
                    EditorGUILayout.LabelField("Avaliação armazenada", evaluation != null ? "disponível" : "indisponível");
                    if (evaluation != null && !string.IsNullOrWhiteSpace(evaluation.Diagnostic))
                    {
                        EditorGUILayout.LabelField("Diagnóstico", evaluation.Diagnostic, EditorStyles.wordWrappedLabel);
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPlanning(RuntimeView view)
        {
            EditorGUILayout.LabelField("Plano e Actions", EditorStyles.largeLabel);
            _showPlanDetails = EditorGUILayout.ToggleLeft("Detalhes técnicos dos planos", _showPlanDetails);
            EditorGUILayout.LabelField("Plano ativo agora", EditorStyles.boldLabel);
            if (!view.HasActivePlan)
            {
                EditorGUILayout.HelpBox("Nenhum plano ativo consta no snapshot atual do Autonomy.", MessageType.Info);
            }
            else
            {
                DrawPlanSummary(view.ActivePlan, "Ativo agora");
                if (_showPlanDetails)
                {
                    DrawPlanDetails(view.ActivePlan);
                }
            }

            PEGANpcPlanSelectionResult selection = view.LatestSelection;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Último resultado armazenado do planejador", EditorStyles.boldLabel);
            if (selection == null)
            {
                EditorGUILayout.LabelField("<nenhuma avaliação armazenada>");
            }
            else
            {
                EditorGUILayout.LabelField("Estado / contexto", $"{(selection.IsValid ? "Válido" : "Inválido")} / {selection.Context}");
                EditorGUILayout.LabelField("Plano escolhido nessa avaliação", selection.HasSelectedPlan ? "sim" : "não");
                _showCausalDetails = EditorGUILayout.ToggleLeft("Exibir diagnósticos e sequências causais", _showCausalDetails);
                if (_showCausalDetails && !string.IsNullOrWhiteSpace(selection.Diagnostic))
                {
                    EditorGUILayout.LabelField("Diagnóstico da seleção", selection.Diagnostic, EditorStyles.wordWrappedLabel);
                }

                if (selection.HasSelectedPlan)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Escolhido na última avaliação (não confirma execução atual)", EditorStyles.boldLabel);
                    DrawPlanSummary(selection.SelectedPlan, "Resumo");
                    if (_showPlanDetails)
                    {
                        DrawPlanDetails(selection.SelectedPlan);
                    }

                    EditorGUILayout.EndVertical();
                }

                IReadOnlyList<PEGANpcPlanSnapshot> candidates = selection.CandidatePlans;
                EditorGUILayout.LabelField($"Candidatos da última avaliação ({candidates.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < candidates.Count; i++)
                {
                    PEGANpcPlanSnapshot candidate = candidates[i];
                    bool isSelected = selection.HasSelectedPlan && AreSamePlan(candidate, selection.SelectedPlan);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(isSelected ? "Escolhido" : $"Candidato {i + 1}", EditorStyles.boldLabel);
                    DrawPlanSummary(candidate, "Resumo");
                    if (_showPlanDetails)
                    {
                        DrawPlanDetails(candidate);
                    }

                    EditorGUILayout.EndVertical();
                }

                DrawCausalEvaluation(selection.CausalEvaluation, _showCausalDetails);
            }
        }

        private static void DrawPlanSummary(PEGANpcPlanSnapshot plan, string heading)
        {
            EditorGUILayout.LabelField(heading, $"{plan.DesireName} · {FormatActionName(plan.ActionId)} · {FormatTargetKind(plan.TargetKind)}");
        }

        private static void DrawPlanDetails(PEGANpcPlanSnapshot plan)
        {
            EditorGUILayout.LabelField("Desire ID", plan.DesireId);
            EditorGUILayout.LabelField("Action ID", plan.ActionId.IsValid ? plan.ActionId.StableText : "<indisponível>");
            EditorGUILayout.LabelField("Target ID", plan.TargetId.IsValid ? plan.TargetId.ToString() : "<indisponível>");
            EditorGUILayout.LabelField("Tipo / proveniência", $"{plan.TargetKind.StableText} / {plan.TargetProvenance}");
            EditorGUILayout.LabelField("Posição conhecida / evidência", $"{plan.TargetPosition:F2} / t={plan.EvidenceTimeSeconds:0.###}s");
            EditorGUILayout.LabelField("Finalidade / efeito previsto", $"{plan.Purpose} / {plan.PredictedEffect.StableText}");
            EditorGUILayout.LabelField("Rota / velocidade efetiva", $"{plan.RouteLengthMeters:0.##} m / {plan.EffectiveSpeedMetersPerSecond:0.##} m/s");
            EditorGUILayout.LabelField("Duração intrínseca / estimada", $"{plan.IntrinsicDurationSeconds:0.##} s / {plan.EstimatedSeconds:0.##} s");
            EditorGUILayout.LabelField("Peso efetivo / preferência de custo / score", $"{plan.EffectiveDesireWeight:0.###} / {plan.CostPreference:0.###} / {plan.Score:0.###}");
            EditorGUILayout.LabelField("Regime de velocidade / distância máxima", $"{plan.SpeedRegime} / {plan.MaximumRouteDistanceFromAnchorMeters:0.##} m");
            EditorGUILayout.LabelField("Effects causais declarados", plan.CausalEffects.Count.ToString());
            for (int i = 0; i < plan.CausalEffects.Count; i++)
            {
                EditorGUILayout.LabelField($"  {i + 1}. {plan.CausalEffects[i].GetType().Name}");
            }
        }

        private static void DrawCausalEvaluation(PEGANpcCausalPlanEvaluation causal, bool showDetails)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Busca causal e alternativas", EditorStyles.boldLabel);
            if (causal == null)
            {
                EditorGUILayout.LabelField("<não exposta neste resultado>");
                return;
            }

            EditorGUILayout.LabelField($"Goals pesquisados: {causal.GoalSearches.Count} · busca incompleta: {causal.HasIncompleteSearch}");
            for (int i = 0; i < causal.GoalSearches.Count; i++)
            {
                PEGANpcCausalGoalSearchEvaluation search = causal.GoalSearches[i];
                GoapSearchAlternativesResult result = search.Search;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{search.DesireId} · {result.Status} · {search.Alternatives.Count} alternativa(s)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Avaliações: {result.CandidateEvaluations} / {result.CandidateEvaluationBudget}" +
                                           (search.HasIncompleteStepEvaluation ? " · etapa incompleta" : string.Empty), EditorStyles.miniLabel);
                if (showDetails)
                {
                    if (!string.IsNullOrWhiteSpace(search.Diagnostic))
                    {
                        EditorGUILayout.LabelField("Diagnóstico", search.Diagnostic, EditorStyles.wordWrappedLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(result.StepEvaluationDiagnostic))
                    {
                        EditorGUILayout.LabelField("Diagnóstico da avaliação de etapas", result.StepEvaluationDiagnostic, EditorStyles.wordWrappedLabel);
                    }
                }

                for (int alternativeIndex = 0; alternativeIndex < search.Alternatives.Count; alternativeIndex++)
                {
                    DrawCausalPlan(search.Alternatives[alternativeIndex], alternativeIndex + 1, showDetails);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawCausalPlan(PEGANpcCausalPlanAlternative alternative, int index, bool showDetails)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{index}. {alternative.DesireName}", EditorStyles.boldLabel, GUILayout.Width(130f));
            EditorGUILayout.LabelField($"{alternative.Plan.Steps.Count} Action(s) · score {alternative.Score:0.###} · {alternative.EstimatedSeconds:0.##} s");
            EditorGUILayout.EndHorizontal();
            if (showDetails)
            {
                EditorGUILayout.LabelField("Condição alvo", alternative.Plan.Goal.TargetCondition.GetType().Name);
                DrawTarget(alternative.Target);
                for (int stepIndex = 0; stepIndex < alternative.Plan.Steps.Count; stepIndex++)
                {
                    string action = stepIndex < alternative.ActionSteps.Count
                        ? $"{FormatActionName(alternative.ActionSteps[stepIndex].ActionId)} ({alternative.ActionSteps[stepIndex].ActionId.StableText})"
                        : "<Action PEGA não mapeada>";
                    EditorGUILayout.LabelField($"  {stepIndex + 1}. {action}");
                }
            }
        }

        private void DrawActions(RuntimeView view)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Definitions configuradas: {view.Autonomy.ActionDefinitionCount}", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Os assets abaixo são configuração. O estado de execução vem dos providers runtime.",
                MessageType.None);
            for (int i = 0; i < view.Autonomy.ActionDefinitionCount; i++)
            {
                PEGANpcActionDefinitionAsset definition = view.Autonomy.GetActionDefinitionAt(i);
                EditorGUILayout.LabelField($"{i + 1}. {(definition != null ? definition.name : "<asset nulo>")}");
            }

            EditorGUILayout.LabelField($"Providers runtime: {view.ActionProviders.Count}", EditorStyles.miniLabel);
            _showActionDetails = EditorGUILayout.ToggleLeft("Exibir contratos, Targets e diagnósticos", _showActionDetails);
            if (view.ActionProviders.Count == 0)
            {
                EditorGUILayout.LabelField("<nenhum provider runtime disponível>");
            }

            for (int i = 0; i < view.ActionProviders.Count; i++)
            {
                IPEGANpcActionProvider provider = view.ActionProviders[i];
                PEGANpcActionExecutionSnapshot execution = provider.CurrentExecution;
                PEGANpcActionContract contract = provider.Contract;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string actionId = contract.ActionId.IsValid ? contract.ActionId.StableText : "Action ID indisponível";
                EditorGUILayout.LabelField(FormatActionName(contract.ActionId), EditorStyles.boldLabel);
                DrawExecutionState(provider.Outcome, execution.Outcome, contract.ActionId);
                EditorGUILayout.LabelField("Efeito observado", execution.EffectObserved ? "sim" : "não");
                if (_showActionDetails)
                {
                    EditorGUILayout.LabelField("Action ID", actionId);
                    EditorGUILayout.LabelField("Request", execution.RequestId.ToString());
                    EditorGUILayout.LabelField("Efeito / tipo de Target", $"{contract.EffectKind.StableText} / {contract.TargetKind.StableText}");
                    EditorGUILayout.LabelField("Requer navegação", contract.RequiresNavigation.ToString());
                    PEGANpcActionEffectDeclaration effect = execution.DeclaredEffect;
                    EditorGUILayout.LabelField("Target do efeito", effect.IsTargetResolved ? effect.TargetId.ToString() : "<não resolvido>");
                    EditorGUILayout.LabelField("Posição / raio", effect.IsTargetResolved ? $"{effect.TargetPosition:F2} / {effect.TargetRadiusMeters:0.##} m" : "<indisponível>");
                    EditorGUILayout.LabelField("Mensagem de início", contract.StartMessage);
                    if (!string.IsNullOrWhiteSpace(execution.Diagnostic))
                    {
                        EditorGUILayout.LabelField("Diagnóstico da execução", execution.Diagnostic, EditorStyles.wordWrappedLabel);
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawKnowledge(RuntimeView view)
        {
            EditorGUILayout.LabelField("Knowledge State individual", EditorStyles.largeLabel);
            if (view.KnowledgeState == null)
            {
                EditorGUILayout.HelpBox("PEGANpcKnowledgeState indisponível no NPC resolvido.", MessageType.Warning);
                return;
            }

            if (!view.KnowledgeReady)
            {
                EditorGUILayout.HelpBox("O armazenamento do Knowledge State não está inicializado; nenhuma consulta foi feita para evitar inicialização pelo Inspector.", MessageType.Info);
                return;
            }

            _showKnowledgeMetadata = EditorGUILayout.ToggleLeft("Exibir IDs, proveniência e tempos", _showKnowledgeMetadata);
            DrawKnowledgeFact(
                "Presença na área percebida",
                view.KnowledgeState.GetPerceivedAreaPresence(),
                FormatAreaPresence,
                _showKnowledgeMetadata,
                true);
            if (view.KnownActors.Count == 0)
            {
                EditorGUILayout.LabelField("Nenhum Actor consta no índice atual de sujeitos conhecidos.");
                return;
            }

            for (int i = 0; i < view.KnownActors.Count; i++)
            {
                ActorId actorId = view.KnownActors[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Sujeito conhecido {i + 1}", EditorStyles.boldLabel);
                if (_showKnowledgeMetadata)
                {
                    EditorGUILayout.LabelField("ActorId", actorId.StableText, EditorStyles.miniLabel);
                }

                DrawKnowledgeFact(
                    "Identidade de personagem",
                    view.KnowledgeState.GetObservedCharacterIdentity(actorId),
                    FormatProfileId,
                    _showKnowledgeMetadata,
                    _showKnowledgeMetadata);
                DrawKnowledgeFact(
                    "Contato visual",
                    view.KnowledgeState.GetCharacterVisualContact(actorId),
                    value => value ? "sim" : "não",
                    _showKnowledgeMetadata,
                    true);
                DrawKnowledgeFact(
                    "Última posição observada",
                    view.KnowledgeState.GetLastObservedCharacterPosition(actorId),
                    value => value.ToString("F2"),
                    _showKnowledgeMetadata,
                    true);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawGangKnowledge(RuntimeView view)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Knowledge State compartilhado da Gangue", EditorStyles.boldLabel);
            PEGANpcGangMembership membership = view.Autonomy.GetComponent<PEGANpcGangMembership>();
            PEGANpcGangKnowledgeState gang = membership != null ? membership.GangKnowledgeState : null;
            if (gang == null)
            {
                EditorGUILayout.LabelField("<NPC sem GangKnowledgeState associado>");
                return;
            }

            if (!PEGAGoapRuntimeInspectorReader.IsGangKnowledgeReady(gang))
            {
                EditorGUILayout.HelpBox("O estado compartilhado ainda não foi inicializado; nenhuma consulta foi feita.", MessageType.Info);
                return;
            }

            IReadOnlyList<PEGANpcPlanningTarget> targets = gang.GetKnownEscapeTargets();
            EditorGUILayout.LabelField("Targets de fuga conhecidos pela Gangue", targets.Count.ToString());
            _showGangTargetDetails = EditorGUILayout.ToggleLeft("Exibir IDs, proveniência e tempos dos Targets", _showGangTargetDetails);
            if (targets.Count == 0)
            {
                EditorGUILayout.LabelField("<nenhum Target registrado>");
            }

            for (int i = 0; i < targets.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Destino de fuga {i + 1} · {targets[i].KnownPosition:F2}", EditorStyles.boldLabel);
                if (_showGangTargetDetails)
                {
                    DrawTarget(targets[i]);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawHistory()
        {
            EditorGUILayout.LabelField("Histórico observado", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Até 40 mudanças • amostragem a cada 0,25 s do Editor • histórico temporário", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Transições mais rápidas que a amostragem podem não aparecer.", EditorStyles.miniLabel);
            if (_history.Count == 0)
            {
                EditorGUILayout.LabelField("<aguardando a primeira amostra>");
                return;
            }

            for (int i = _history.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.LabelField(_history[i], EditorStyles.helpBox);
            }
        }

        private void Observe(PEGANpcAutonomy autonomy)
        {
            RuntimeView view = PEGAGoapRuntimeInspectorReader.Read(autonomy);
            string activePlan = view.HasActivePlan ? FormatPlanIdentity(view.ActivePlan) : "<sem plano ativo>";
            string selection = view.LatestSelection == null
                ? "<sem seleção armazenada>"
                : view.LatestSelection.HasSelectedPlan
                    ? FormatPlanIdentity(view.LatestSelection.SelectedPlan)
                    : $"<sem plano selecionado; válido={view.LatestSelection.IsValid}; {view.LatestSelection.Diagnostic}>";

            if (_previousActivePlan == null)
            {
                AddHistory($"Amostra inicial: plano ativo {activePlan}; última seleção {selection}.");
            }
            else
            {
                if (!string.Equals(_previousActivePlan, activePlan, StringComparison.Ordinal))
                {
                    AddHistory($"Plano ativo: {_previousActivePlan} → {activePlan}");
                }

                if (!string.Equals(_previousSelection, selection, StringComparison.Ordinal))
                {
                    AddHistory($"Seleção armazenada: {_previousSelection} → {selection}");
                }
            }

            _previousActivePlan = activePlan;
            _previousSelection = selection;

            HashSet<string> currentActionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < view.ActionProviders.Count; i++)
            {
                IPEGANpcActionProvider provider = view.ActionProviders[i];
                string id = provider.Contract.ActionId.StableText;
                string state = $"{provider.CurrentExecution.Outcome} / request {provider.CurrentExecution.RequestId}";
                currentActionIds.Add(id);
                if (_previousExecutions.TryGetValue(id, out string previous) && !string.Equals(previous, state, StringComparison.Ordinal))
                {
                    AddHistory($"Action {id}: {previous} → {state}");
                }

                _previousExecutions[id] = state;
            }

            List<string> removed = new List<string>();
            foreach (KeyValuePair<string, string> pair in _previousExecutions)
            {
                if (!currentActionIds.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int i = 0; i < removed.Count; i++)
            {
                _previousExecutions.Remove(removed[i]);
            }
        }

        private void AddHistory(string message)
        {
            _history.Add($"t={Time.timeAsDouble:0.00}s  {message}");
            while (_history.Count > MaximumHistoryEntries)
            {
                _history.RemoveAt(0);
            }
        }

        private void ResetObservation()
        {
            _history.Clear();
            _previousActivePlan = null;
            _previousSelection = null;
            _previousExecutions.Clear();
            _nextSampleAt = 0d;
        }

        private static PEGANpcAutonomy ResolveSelectedAutonomy()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<PEGANpcAutonomy>(true) : null;
        }

        private static PEGANpcDesirePlanEvaluation FindDesireEvaluation(
            IReadOnlyList<PEGANpcDesirePlanEvaluation> evaluations,
            string desireId)
        {
            for (int i = 0; i < evaluations.Count; i++)
            {
                if (string.Equals(evaluations[i].DesireId, desireId, StringComparison.Ordinal))
                {
                    return evaluations[i];
                }
            }

            return null;
        }

        private static bool AreSamePlan(PEGANpcPlanSnapshot left, PEGANpcPlanSnapshot right) =>
            string.Equals(left.DesireId, right.DesireId, StringComparison.Ordinal) &&
            left.ActionId == right.ActionId &&
            left.TargetId == right.TargetId;

        private static string FindConfiguredGoalName(
            PEGANpcDesireProfile profile,
            string desireId,
            PEGANpcPlanSelectionResult latestSelection)
        {
            if (profile == null)
            {
                return "<Profile indisponível>";
            }

            PEGANpcDesireContext context = latestSelection != null
                ? latestSelection.Context
                : PEGANpcDesireContext.Normal;
            if (!profile.TryGetContext(context, out PEGANpcDesireContextDefinition definition, out _))
            {
                return "<Goal configurado indisponível>";
            }

            IReadOnlyList<PEGANpcDesireConfiguration> desires = definition.Desires;
            for (int i = 0; i < desires.Count; i++)
            {
                PEGANpcDesireDefinition desire = desires[i] != null ? desires[i].Definition : null;
                if (desire != null && string.Equals(desire.DesireId, desireId, StringComparison.Ordinal))
                {
                    return desire.DesiredGoalCondition != null
                        ? desire.DesiredGoalCondition.name
                        : "<condição não configurada>";
                }
            }

            return "<Goal não encontrado no Profile atual>";
        }

        private static IPEGANpcActionProvider FindProvider(
            IReadOnlyList<IPEGANpcActionProvider> providers,
            PEGANpcPlanningActionId actionId)
        {
            for (int i = 0; i < providers.Count; i++)
            {
                if (providers[i].Contract.ActionId == actionId)
                {
                    return providers[i];
                }
            }

            return null;
        }

        private static string FormatActionName(PEGANpcPlanningActionId actionId)
        {
            if (actionId == PEGANpcPlanningActionId.ApproachKnownCharacter)
            {
                return "Aproximar personagem conhecido";
            }

            if (actionId == PEGANpcPlanningActionId.TravelToEscapeTarget)
            {
                return "Ir para saída conhecida";
            }

            if (actionId == PEGANpcPlanningActionId.InvestigateLastKnownCharacterPosition)
            {
                return "Investigar última posição conhecida";
            }

            if (actionId == PEGANpcPlanningActionId.MaintainSafeSeparationFromKnownCharacter)
            {
                return "Manter distância segura";
            }

            if (actionId == PEGANpcPlanningActionId.InvestigatePerceivedAreaPresence)
            {
                return "Investigar presença percebida";
            }

            return actionId.IsValid ? actionId.StableText : "Action indisponível";
        }

        private static string FormatTargetKind(PEGANpcPlanningTargetKind targetKind)
        {
            if (targetKind == PEGANpcPlanningTargetKind.CharacterPosition)
            {
                return "posição de personagem";
            }

            if (targetKind == PEGANpcPlanningTargetKind.EscapeDestination)
            {
                return "saída";
            }

            if (targetKind == PEGANpcPlanningTargetKind.LocalObservationPosition)
            {
                return "ponto de investigação";
            }

            if (targetKind == PEGANpcPlanningTargetKind.SafeSeparationPosition)
            {
                return "posição de afastamento";
            }

            return targetKind.IsValid ? targetKind.StableText : "Target indisponível";
        }

        private static string FormatOutcome(PEGANpcActionOutcome outcome)
        {
            string label = outcome switch
            {
                PEGANpcActionOutcome.NotStarted => "Não iniciada",
                PEGANpcActionOutcome.Running => "Em execução",
                PEGANpcActionOutcome.Success => "Concluída",
                PEGANpcActionOutcome.Invalid => "Inválida",
                PEGANpcActionOutcome.Interrupted => "Interrompida",
                PEGANpcActionOutcome.Cancelled => "Cancelada",
                _ => "Desconhecida"
            };
            return $"{label} ({outcome})";
        }

        private static string FormatPlanIdentity(PEGANpcPlanSnapshot plan) =>
            $"Goal={plan.DesireId}; Action={plan.ActionId.StableText}; Target={plan.TargetId}";

        private static void DrawTarget(PEGANpcPlanningTarget target)
        {
            EditorGUILayout.LabelField("Identidade", target.TargetId.IsValid ? target.TargetId.ToString() : "<indisponível>");
            EditorGUILayout.LabelField("Tipo / proveniência", $"{target.Kind.StableText} / {target.Provenance}");
            EditorGUILayout.LabelField("Posição conhecida", target.KnownPosition.ToString("F2"));
            EditorGUILayout.LabelField("Tempo da evidência", target.HasEvidenceTime ? $"{target.EvidenceTimeSeconds:0.###} s" : "<não fornecido>");
        }

        private static void DrawKnowledgeFact<T>(
            string label,
            PEGANpcKnowledgeFact<T> fact,
            Func<T, string> formatValue,
            bool showMetadata,
            bool showValue)
        {
            string value = fact.TryGetValue(out T observedValue)
                ? showValue ? formatValue(observedValue) : "<valor disponível nos detalhes técnicos>"
                : "<sem valor conhecido>";
            EditorGUILayout.LabelField(label, $"{fact.Status}: {value}");
            if (showMetadata)
            {
                string metadata = $"origem={fact.Origins}; invalidação={fact.InvalidationCauses}";
                if (fact.HasObservationTime)
                {
                    metadata += $"; observação t={fact.ObservationTimeSeconds:0.###}s";
                }

                EditorGUILayout.LabelField("  Proveniência / tempo", metadata, EditorStyles.miniLabel);
            }
        }

        private static string FormatProfileId(ActorProfileId id) => id.IsValid ? id.StableText : "<identidade inválida>";

        private static string FormatAreaPresence(PEGANpcPerceivedAreaPresence presence) =>
            $"presença={presence.IsPresent}; posição do observador={presence.ObserverPosition:F2}; raio={presence.PerceivedAreaRadiusMeters:0.##} m";
    }

    internal sealed class RuntimeView
    {
        public PEGANpcAutonomy Autonomy;
        public PEGANpcDesireProfile Profile;
        public PEGANpcDesireStateSnapshot DesireState;
        public bool HasActivePlan;
        public PEGANpcPlanSnapshot ActivePlan;
        public PEGANpcPlanSelectionResult LatestSelection;
        public readonly List<IPEGANpcActionProvider> ActionProviders = new List<IPEGANpcActionProvider>();
        public PEGANpcKnowledgeState KnowledgeState;
        public bool KnowledgeReady;
        public readonly List<ActorId> KnownActors = new List<ActorId>();
        public readonly List<string> Issues = new List<string>();
    }

    /// <summary>Reads existing PEGA snapshots; reflection is confined to this Editor assembly for private retained state.</summary>
    internal static class PEGAGoapRuntimeInspectorReader
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo DesireProfileField = typeof(PEGANpcAutonomy).GetField("desireProfile", InstancePrivate);
        private static readonly FieldInfo DesireStateField = typeof(PEGANpcAutonomy).GetField("_desireState", InstancePrivate);
        private static readonly FieldInfo HasActivePlanField = typeof(PEGANpcAutonomy).GetField("_hasActivePlan", InstancePrivate);
        private static readonly FieldInfo ActivePlanField = typeof(PEGANpcAutonomy).GetField("_activePlan", InstancePrivate);
        private static readonly FieldInfo LatestSelectionField = typeof(PEGANpcAutonomy).GetField("_latestSelection", InstancePrivate);
        private static readonly FieldInfo ActionProvidersField = typeof(PEGANpcAutonomy).GetField("_actionProviders", InstancePrivate);
        private static readonly FieldInfo KnowledgeSubjectsField = typeof(PEGANpcKnowledgeState).GetField("_knowledgeBySubject", InstancePrivate);
        private static readonly FieldInfo PerceivedAreaField = typeof(PEGANpcKnowledgeState).GetField("_perceivedAreaPresence", InstancePrivate);
        private static readonly FieldInfo GangTargetsField = typeof(PEGANpcGangKnowledgeState).GetField("_knownEscapePoints", InstancePrivate);

        public static RuntimeView Read(PEGANpcAutonomy autonomy)
        {
            RuntimeView view = new RuntimeView { Autonomy = autonomy };
            view.Profile = ReadField<PEGANpcDesireProfile>(DesireProfileField, autonomy, "Desire Profile", view.Issues);
            PEGANpcDesireState desireState = ReadField<PEGANpcDesireState>(DesireStateField, autonomy, "estado runtime dos Desejos", view.Issues);
            if (desireState != null)
            {
                view.DesireState = desireState.GetSnapshot();
            }

            view.HasActivePlan = ReadField<bool>(HasActivePlanField, autonomy, "flag do plano ativo", view.Issues);
            view.ActivePlan = ReadField<PEGANpcPlanSnapshot>(ActivePlanField, autonomy, "snapshot do plano ativo", view.Issues);
            view.LatestSelection = ReadField<PEGANpcPlanSelectionResult>(LatestSelectionField, autonomy, "último resultado de seleção", view.Issues);
            ReadActionProviders(autonomy, view);

            view.KnowledgeState = autonomy.GetComponent<PEGANpcKnowledgeState>();
            if (view.KnowledgeState != null)
            {
                object subjects = ReadRawField(KnowledgeSubjectsField, view.KnowledgeState, "índice de sujeitos do Knowledge State", view.Issues);
                object perceivedArea = ReadRawField(PerceivedAreaField, view.KnowledgeState, "fato de presença percebida", view.Issues);
                view.KnowledgeReady = subjects is IDictionary && perceivedArea != null;
                if (subjects is IDictionary dictionary)
                {
                    foreach (object key in dictionary.Keys)
                    {
                        if (key is ActorId actorId && actorId.IsValid)
                        {
                            view.KnownActors.Add(actorId);
                        }
                    }

                    view.KnownActors.Sort((left, right) => string.CompareOrdinal(left.StableText, right.StableText));
                }
            }
            else
            {
                view.Issues.Add("Componente PEGANpcKnowledgeState não encontrado no NPC resolvido.");
            }

            return view;
        }

        public static bool IsGangKnowledgeReady(PEGANpcGangKnowledgeState gang)
        {
            object targets = ReadRawField(GangTargetsField, gang, "índice de Targets compartilhados", null);
            return targets is IDictionary;
        }

        private static void ReadActionProviders(PEGANpcAutonomy autonomy, RuntimeView view)
        {
            object raw = ReadRawField(ActionProvidersField, autonomy, "lista de Action providers", view.Issues);
            if (raw is IEnumerable providers)
            {
                foreach (object item in providers)
                {
                    if (item is IPEGANpcActionProvider provider)
                    {
                        view.ActionProviders.Add(provider);
                    }
                }
            }
        }

        private static T ReadField<T>(FieldInfo field, object target, string label, List<string> issues)
        {
            object value = ReadRawField(field, target, label, issues);
            if (value is T typed)
            {
                return typed;
            }

            return default;
        }

        private static object ReadRawField(FieldInfo field, object target, string label, List<string> issues)
        {
            if (field == null)
            {
                issues?.Add($"Campo interno de {label} não localizado; a janela pode precisar de atualização para a versão atual do PEGA.");
                return null;
            }

            try
            {
                return field.GetValue(target);
            }
            catch (Exception exception)
            {
                issues?.Add($"Não foi possível ler {label}: {exception.GetType().Name}: {exception.Message}");
                return null;
            }
        }
    }
}
