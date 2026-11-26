using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Autonomy;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Desires;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using EditorSelection = UnityEditor.Selection;
using UnityEditor;
using UnityEngine;

namespace PEGA.Diagnostics.Editor
{
    /// <summary>Editor-only, read-only view of the GOAP snapshots retained by a selected NPC.</summary>
    public sealed class PegaGoapRuntimeInspectorWindow : EditorWindow
    {
        private const double SampleIntervalSeconds = 0.25d;
        private const int MaximumHistoryEntries = 40;
        private static readonly string[] TabNames =
        {
            "Geral", "Desejos", "Plano", "Conhecimento", "Histórico"
        };

        private readonly List<string> _history = new List<string>();
        private PegaNpcAutonomy _observedAutonomy;
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
        private bool _showKnowledgeEvidenceDetails;

        [MenuItem("PEGA/Diagnostics/GOAP Runtime Inspector")]
        private static void OpenWindow()
        {
            GetWindow<PegaGoapRuntimeInspectorWindow>("GOAP Runtime Inspector");
        }

        private void OnEnable()
        {
            EditorSelection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            EditorSelection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnSelectionChanged()
        {
            PegaNpcAutonomy selected = ResolveSelectedAutonomy();
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
            PegaNpcAutonomy selected = ResolveSelectedAutonomy();
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
            PegaNpcAutonomy autonomy = ResolveSelectedAutonomy();
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

            RuntimeView view = PegaGoapRuntimeInspectorReader.Read(autonomy);
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
                    break;
                case 4:
                    DrawHistory();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSelection(PegaNpcAutonomy autonomy)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GOAP Runtime Inspector", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(EditorApplication.isPlaying ? "PLAY MODE" : "EDIT MODE", EditorStyles.miniBoldLabel, GUILayout.Width(75f));
            EditorGUILayout.EndHorizontal();

            GameObject selected = EditorSelection.activeGameObject;
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
            if (view.issues.Count == 0)
            {
                return;
            }

            for (int i = 0; i < view.issues.Count; i++)
            {
                EditorGUILayout.HelpBox(view.issues[i], MessageType.Warning);
            }
        }

        private void DrawOverview(RuntimeView view)
        {
            EditorGUILayout.LabelField("Visão geral · agora", EditorStyles.largeLabel);
            if (view.hasActivePlan)
            {
                PegaNpcPlanSnapshot plan = view.activePlan;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Desejo selecionado no plano ativo", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(plan.DesireName) ? "Nome não exposto" : plan.DesireName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Goal configurado", FindConfiguredGoalName(view.profile, plan.DesireId, view.latestSelection));
                EditorGUILayout.LabelField("Verdade atual do Goal", "Não exposta pelo runtime", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Action ativa", FormatActionName(plan.ActionId));
                EditorGUILayout.LabelField("Target / posição conhecida", plan.TargetId.IsValid ? $"{FormatTargetKind(plan.TargetKind)} · {plan.TargetPosition:F2}" : "<Target indisponível>");

                IPegaNpcActionProvider provider = FindProvider(view.actionProviders, plan.ActionId);
                if (provider == null)
                {
                    EditorGUILayout.HelpBox("O snapshot contém um plano ativo, mas seu provider de execução não está disponível para leitura.", MessageType.Warning);
                }
                else
                {
                    PegaNpcActionExecutionSnapshot execution = provider.CurrentExecution;
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
                DrawRunningProvidersWithoutPlan(view.actionProviders);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Último resultado do planejador", EditorStyles.boldLabel);
            if (view.latestSelection == null)
            {
                EditorGUILayout.LabelField("Nenhum resultado armazenado.");
                return;
            }

            PegaNpcPlanSelectionResult selection = view.latestSelection;
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
            PegaNpcActionOutcome providerOutcome,
            PegaNpcActionOutcome snapshotOutcome,
            PegaNpcPlanningActionId actionId)
        {
            MessageType messageType = providerOutcome == PegaNpcActionOutcome.Invalid ||
                                     providerOutcome == PegaNpcActionOutcome.Interrupted
                ? MessageType.Warning
                : providerOutcome == PegaNpcActionOutcome.Running
                    ? MessageType.Info
                    : MessageType.None;
            EditorGUILayout.HelpBox(
                $"Execução observada · {FormatActionName(actionId)}: {FormatOutcome(providerOutcome)} · snapshot: {FormatOutcome(snapshotOutcome)}",
                messageType);
        }

        private static void DrawRunningProvidersWithoutPlan(IReadOnlyList<IPegaNpcActionProvider> providers)
        {
            bool foundRunningProvider = false;
            for (int i = 0; i < providers.Count; i++)
            {
                IPegaNpcActionProvider provider = providers[i];
                if (provider.Outcome == PegaNpcActionOutcome.Running)
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
            if (view.profile == null)
            {
                EditorGUILayout.HelpBox("Desire Profile indisponível no componente selecionado.", MessageType.Info);
                return;
            }

            PegaNpcDesireContext context = view.latestSelection != null
                ? view.latestSelection.Context
                : PegaNpcDesireContext.Normal;
            EditorGUILayout.LabelField("Profile / contexto", $"{view.profile.name} / {context}", EditorStyles.miniLabel);
            if (!view.profile.TryGetContext(context, out PegaNpcDesireContextDefinition definition, out string diagnostic))
            {
                EditorGUILayout.HelpBox($"Configuração de Desejos indisponível: {diagnostic}", MessageType.Warning);
                return;
            }

            PegaNpcDesireStateSnapshot satisfaction = view.desireState;
            IReadOnlyList<PegaNpcDesirePlanEvaluation> evaluations = view.latestSelection != null
                ? view.latestSelection.DesireEvaluations
                : Array.Empty<PegaNpcDesirePlanEvaluation>();
            EditorGUILayout.HelpBox(
                "Aplicabilidade e peso efetivo vêm da última avaliação armazenada. A verdade lógica atual do Goal não é exposta pelo runtime.",
                MessageType.None);
            _showDesireDetails = EditorGUILayout.ToggleLeft("Exibir IDs, configuração e diagnósticos", _showDesireDetails);

            IReadOnlyList<PegaNpcDesireConfiguration> desires = definition.Desires;
            if (desires.Count == 0)
            {
                EditorGUILayout.LabelField("Nenhum Desejo configurado para este contexto.");
                return;
            }

            for (int i = 0; i < desires.Count; i++)
            {
                PegaNpcDesireConfiguration configuration = desires[i];
                if (configuration == null || configuration.Definition == null)
                {
                    EditorGUILayout.HelpBox($"Desire Setting {i}: definição indisponível.", MessageType.Warning);
                    continue;
                }

                PegaNpcDesireDefinition desire = configuration.Definition;
                PegaNpcDesireSatisfactionSnapshot current = default;
                bool hasSatisfaction = satisfaction != null && satisfaction.TryGet(desire.DesireId, out current);
                PegaNpcDesirePlanEvaluation evaluation = FindDesireEvaluation(evaluations, desire.DesireId);
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
            if (!view.hasActivePlan)
            {
                EditorGUILayout.HelpBox("Nenhum plano ativo consta no snapshot atual do Autonomy.", MessageType.Info);
            }
            else
            {
                DrawPlanSummary(view.activePlan, "Ativo agora");
                if (_showPlanDetails)
                {
                    DrawPlanDetails(view.activePlan);
                }
            }

            PegaNpcPlanSelectionResult selection = view.latestSelection;
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

                IReadOnlyList<PegaNpcPlanSnapshot> candidates = selection.CandidatePlans;
                EditorGUILayout.LabelField($"Candidatos da última avaliação ({candidates.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < candidates.Count; i++)
                {
                    PegaNpcPlanSnapshot candidate = candidates[i];
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

        private static void DrawPlanSummary(PegaNpcPlanSnapshot plan, string heading)
        {
            EditorGUILayout.LabelField(heading, $"{plan.DesireName} · {FormatActionName(plan.ActionId)} · {FormatTargetKind(plan.TargetKind)}");
        }

        private static void DrawPlanDetails(PegaNpcPlanSnapshot plan)
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

        private static void DrawCausalEvaluation(PegaNpcCausalPlanEvaluation causal, bool showDetails)
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
                PegaNpcCausalGoalSearchEvaluation search = causal.GoalSearches[i];
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

        private static void DrawCausalPlan(PegaNpcCausalPlanAlternative alternative, int index, bool showDetails)
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
            EditorGUILayout.LabelField($"Definitions configuradas: {view.autonomy.ActionDefinitionCount}", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Os assets abaixo são configuração. O estado de execução vem dos providers runtime.",
                MessageType.None);
            for (int i = 0; i < view.autonomy.ActionDefinitionCount; i++)
            {
                PegaNpcActionDefinitionAsset definition = view.autonomy.GetActionDefinitionAt(i);
                EditorGUILayout.LabelField($"{i + 1}. {(definition != null ? definition.name : "<asset nulo>")}");
            }

            EditorGUILayout.LabelField($"Providers runtime: {view.actionProviders.Count}", EditorStyles.miniLabel);
            _showActionDetails = EditorGUILayout.ToggleLeft("Exibir contratos, Targets e diagnósticos", _showActionDetails);
            if (view.actionProviders.Count == 0)
            {
                EditorGUILayout.LabelField("<nenhum provider runtime disponível>");
            }

            for (int i = 0; i < view.actionProviders.Count; i++)
            {
                IPegaNpcActionProvider provider = view.actionProviders[i];
                PegaNpcActionExecutionSnapshot execution = provider.CurrentExecution;
                PegaNpcActionContract contract = provider.Contract;
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
                    PegaNpcActionEffectDeclaration effect = execution.DeclaredEffect;
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
            EditorGUILayout.LabelField("Conhecimento disponível ao NPC", EditorStyles.largeLabel);
            if (view.knowledgeState == null)
            {
                EditorGUILayout.HelpBox("PEGANpcKnowledgeState indisponível no NPC resolvido.", MessageType.Warning);
                return;
            }

            PegaKnowledgeRuntimeInspectorSnapshot snapshot =
                PegaKnowledgeRuntimeInspectorSnapshotReader.Read(view.autonomy, view.knowledgeState);
            EditorGUILayout.LabelField(
                $"Consulta em t={snapshot.queryTimeSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} s",
                EditorStyles.miniLabel);
            _showKnowledgeEvidenceDetails = EditorGUILayout.ToggleLeft(
                "Exibir fonte e payload de cada evidência",
                _showKnowledgeEvidenceDetails);

            if (snapshot.hasReadableAuthority && !snapshot.hasRegisteredFacts && !snapshot.hasEnumerationIssues)
            {
                EditorGUILayout.HelpBox(
                    "Nenhuma chave de fato está registrada nas authorities legíveis neste momento. O Inspector não consulta World State nem cria fatos para completar a lista.",
                    MessageType.Info);
            }

            DrawKnowledgeFactSection(
                "Individual · authority do NPC",
                snapshot.individualAvailable,
                snapshot.individualAuthorityStatus,
                snapshot.individualFacts);

            EditorGUILayout.LabelField("Group · authorities coletivas", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(snapshot.groupReaderStatus, EditorStyles.miniLabel);
            if (snapshot.groups.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Nenhum Group com store de Knowledge foi encontrado nas authorities configuradas para este NPC.",
                    MessageType.Info);
            }

            for (int index = 0; index < snapshot.groups.Count; index++)
            {
                PegaKnowledgeRuntimeInspectorGroup group = snapshot.groups[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    string.IsNullOrWhiteSpace(group.displayName) ? group.groupId.StableText : group.displayName,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField("GroupId", group.groupId.StableText);
                EditorGUILayout.LabelField("Acesso", FormatGroupAccess(group.accessStatus));
                if (group.isCurrentMember.HasValue)
                {
                    EditorGUILayout.LabelField(
                        "Membership vigente",
                        group.isCurrentMember.Value ? "confirmada · leitura autorizada" : "não confirmada · leitura negada");
                }

                if (!group.canRead)
                {
                    EditorGUILayout.HelpBox(
                        string.IsNullOrWhiteSpace(group.diagnostic)
                            ? "Acesso negado ou authority indisponível; os fatos deste Group foram ocultados."
                            : group.diagnostic,
                        group.accessStatus == PegaActorGroupKnowledgeAccessStatus.NotMember
                            ? MessageType.Info
                            : MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField(group.diagnostic, EditorStyles.miniLabel);
                    DrawKnowledgeFacts(group.facts);
                }

                EditorGUILayout.EndVertical();
            }

            DrawKnowledgeFactSection(
                "Global · authority da Activity",
                snapshot.globalAvailable,
                snapshot.globalAuthorityStatus,
                snapshot.globalFacts);
        }

        private void DrawKnowledgeFactSection(
            string title,
            bool available,
            string authorityStatus,
            IReadOnlyList<PegaKnowledgeRuntimeInspectorFact> facts)
        {
            EditorGUILayout.LabelField(title, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(authorityStatus, EditorStyles.miniLabel);
            if (!available)
            {
                EditorGUILayout.HelpBox(
                    "Esta authority não está disponível para leitura no runtime atual; seus fatos não foram consultados.",
                    MessageType.Info);
                return;
            }

            DrawKnowledgeFacts(facts);
        }

        private void DrawKnowledgeFacts(IReadOnlyList<PegaKnowledgeRuntimeInspectorFact> facts)
        {
            if (facts == null || facts.Count == 0)
            {
                return;
            }

            for (int index = 0; index < facts.Count; index++)
            {
                DrawKnowledgeFact(facts[index]);
            }
        }

        private void DrawKnowledgeFact(PegaKnowledgeRuntimeInspectorFact fact)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Identidade tipada", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{fact.addressTypeName} · {fact.addressText}",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Escopo", fact.scope.ToString());
            EditorGUILayout.LabelField("Propriedade", fact.definitionName);
            EditorGUILayout.LabelField("ID canônico", fact.definitionId);
            EditorGUILayout.LabelField("Tipo do valor", fact.valueTypeName);
            EditorGUILayout.LabelField("Registro", fact.hasStoredRecord ? "presente nesta authority" : "inexistente neste escopo");
            EditorGUILayout.LabelField("Integridade da definição/endereço", fact.isValid ? "válidos" : "inválidos");
            EditorGUILayout.LabelField("Estado", FormatKnowledgeStatus(fact.status));
            EditorGUILayout.LabelField(
                "Valor",
                fact.hasValue ? fact.valueText : "<sem valor conhecido>",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Validade",
                !fact.isValid
                    ? "inválido · chave ou definição rejeitada"
                    : fact.status == PegaKnowledgeStatus.Known
                        ? "válido no instante da consulta"
                        : fact.status == PegaKnowledgeStatus.Invalidated
                            ? "invalidado pela política/evidência registrada"
                            : fact.hasStoredRecord
                                ? "desconhecido · registro sem valor atualmente conhecido"
                                : "desconhecido · fato não registrado neste escopo");
            EditorGUILayout.LabelField("Origens", FormatKnowledgeOrigins(fact.origins));
            EditorGUILayout.LabelField(
                "Timestamp da observação",
                fact.hasObservationTime
                    ? $"t={fact.observationTimeSeconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} s"
                    : "<não fornecido>");
            if (fact.invalidationCauses != PegaKnowledgeInvalidationCause.None)
            {
                EditorGUILayout.LabelField("Causas de invalidação", fact.invalidationCauses.ToString());
            }

            if (!string.IsNullOrWhiteSpace(fact.diagnostic))
            {
                EditorGUILayout.HelpBox(fact.diagnostic, MessageType.Warning);
            }

            if (_showKnowledgeEvidenceDetails)
            {
                if (fact.evidence.Count == 0)
                {
                    EditorGUILayout.LabelField("Evidências", "<nenhuma>", EditorStyles.miniLabel);
                }
                else
                {
                    for (int index = 0; index < fact.evidence.Count; index++)
                    {
                        PegaKnowledgeRuntimeInspectorEvidence evidence = fact.evidence[index];
                        EditorGUILayout.LabelField(
                            $"Evidência {index + 1}",
                            $"origem={evidence.origin}; t={evidence.observationTimeSeconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} s",
                            EditorStyles.miniLabel);
                        EditorGUILayout.LabelField("  Fonte", evidence.source, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField("  Payload", evidence.payload, EditorStyles.wordWrappedLabel);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static string FormatKnowledgeStatus(PegaKnowledgeStatus status) => status switch
        {
            PegaKnowledgeStatus.Known => "Conhecido",
            PegaKnowledgeStatus.Invalidated => "Invalidado",
            _ => "Desconhecido"
        };

        private static string FormatGroupAccess(PegaActorGroupKnowledgeAccessStatus status) => status switch
        {
            PegaActorGroupKnowledgeAccessStatus.Authorized => "autorizado",
            PegaActorGroupKnowledgeAccessStatus.NotMember => "negado · NPC não é membro vigente",
            PegaActorGroupKnowledgeAccessStatus.GroupUnavailable => "Group inexistente ou não confirmado como vivo",
            PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable => "authority/binding indisponível",
            PegaActorGroupKnowledgeAccessStatus.InvalidActor => "ActorId do NPC inválido ou indisponível",
            PegaActorGroupKnowledgeAccessStatus.AmbiguousAuthority => "múltiplas authorities declaram o Group",
            _ => status.ToString()
        };

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

        private void Observe(PegaNpcAutonomy autonomy)
        {
            RuntimeView view = PegaGoapRuntimeInspectorReader.Read(autonomy);
            string activePlan = view.hasActivePlan ? FormatPlanIdentity(view.activePlan) : "<sem plano ativo>";
            string selection = view.latestSelection == null
                ? "<sem seleção armazenada>"
                : view.latestSelection.HasSelectedPlan
                    ? FormatPlanIdentity(view.latestSelection.SelectedPlan)
                    : $"<sem plano selecionado; válido={view.latestSelection.IsValid}; {view.latestSelection.Diagnostic}>";

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
            for (int i = 0; i < view.actionProviders.Count; i++)
            {
                IPegaNpcActionProvider provider = view.actionProviders[i];
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

        private static PegaNpcAutonomy ResolveSelectedAutonomy()
        {
            GameObject selected = EditorSelection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<PegaNpcAutonomy>(true) : null;
        }

        private static PegaNpcDesirePlanEvaluation FindDesireEvaluation(
            IReadOnlyList<PegaNpcDesirePlanEvaluation> evaluations,
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

        private static bool AreSamePlan(PegaNpcPlanSnapshot left, PegaNpcPlanSnapshot right) =>
            string.Equals(left.DesireId, right.DesireId, StringComparison.Ordinal) &&
            left.ActionId == right.ActionId &&
            left.TargetId == right.TargetId;

        private static string FindConfiguredGoalName(
            PegaNpcDesireProfile profile,
            string desireId,
            PegaNpcPlanSelectionResult latestSelection)
        {
            if (profile == null)
            {
                return "<Profile indisponível>";
            }

            PegaNpcDesireContext context = latestSelection != null
                ? latestSelection.Context
                : PegaNpcDesireContext.Normal;
            if (!profile.TryGetContext(context, out PegaNpcDesireContextDefinition definition, out _))
            {
                return "<Goal configurado indisponível>";
            }

            IReadOnlyList<PegaNpcDesireConfiguration> desires = definition.Desires;
            for (int i = 0; i < desires.Count; i++)
            {
                PegaNpcDesireDefinition desire = desires[i] != null ? desires[i].Definition : null;
                if (desire != null && string.Equals(desire.DesireId, desireId, StringComparison.Ordinal))
                {
                    return desire.DesiredGoalCondition != null
                        ? desire.DesiredGoalCondition.name
                        : "<condição não configurada>";
                }
            }

            return "<Goal não encontrado no Profile atual>";
        }

        private static IPegaNpcActionProvider FindProvider(
            IReadOnlyList<IPegaNpcActionProvider> providers,
            PegaNpcPlanningActionId actionId)
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

        private static string FormatActionName(PegaNpcPlanningActionId actionId)
        {
            if (actionId == PegaNpcPlanningActionId.ApproachKnownCharacter)
            {
                return "Aproximar personagem conhecido";
            }

            if (actionId == PegaNpcPlanningActionId.InvestigateLastKnownCharacterPosition)
            {
                return "Investigar última posição conhecida";
            }

            if (actionId == PegaNpcPlanningActionId.MaintainSafeSeparationFromKnownCharacter)
            {
                return "Manter distância segura";
            }

            if (actionId == PegaNpcPlanningActionId.InvestigatePerceivedAreaPresence)
            {
                return "Investigar presença percebida";
            }

            return actionId.IsValid ? actionId.StableText : "Action indisponível";
        }

        private static string FormatTargetKind(PegaNpcPlanningTargetKind targetKind)
        {
            if (targetKind == PegaNpcPlanningTargetKind.CharacterPosition)
            {
                return "posição de personagem";
            }

            if (targetKind == PegaNpcPlanningTargetKind.LocalObservationPosition)
            {
                return "ponto de investigação";
            }

            if (targetKind == PegaNpcPlanningTargetKind.SafeSeparationPosition)
            {
                return "posição de afastamento";
            }

            return targetKind.IsValid ? targetKind.StableText : "Target indisponível";
        }

        private static string FormatOutcome(PegaNpcActionOutcome outcome)
        {
            string label = outcome switch
            {
                PegaNpcActionOutcome.NotStarted => "Não iniciada",
                PegaNpcActionOutcome.Running => "Em execução",
                PegaNpcActionOutcome.Success => "Concluída",
                PegaNpcActionOutcome.Invalid => "Inválida",
                PegaNpcActionOutcome.Interrupted => "Interrompida",
                PegaNpcActionOutcome.Cancelled => "Cancelada",
                _ => "Desconhecida"
            };
            return $"{label} ({outcome})";
        }

        private static string FormatPlanIdentity(PegaNpcPlanSnapshot plan) =>
            $"Goal={plan.DesireId}; Action={plan.ActionId.StableText}; Target={plan.TargetId}";

        private static void DrawTarget(PegaNpcPlanningTarget target)
        {
            EditorGUILayout.LabelField("Identidade", target.TargetId.IsValid ? target.TargetId.ToString() : "<indisponível>");
            EditorGUILayout.LabelField("Tipo / proveniência", $"{target.Kind.StableText} / {target.Provenance}");
            EditorGUILayout.LabelField("Posição conhecida", target.KnownPosition.ToString("F2"));
            EditorGUILayout.LabelField("Tempo da evidência", target.HasEvidenceTime ? $"{target.EvidenceTimeSeconds:0.###} s" : "<não fornecido>");
        }

        private static string FormatKnowledgeOrigins(IReadOnlyList<PegaKnowledgeEvidenceOrigin> origins)
        {
            if (origins == null || origins.Count == 0)
            {
                return "<nenhuma>";
            }

            var names = new string[origins.Count];
            for (int index = 0; index < origins.Count; index++)
            {
                names[index] = origins[index].ToString();
            }

            return $"[{string.Join(", ", names)}]";
        }
    }

    internal sealed class RuntimeView
    {
        public PegaNpcAutonomy autonomy;
        public PegaNpcDesireProfile profile;
        public PegaNpcDesireStateSnapshot desireState;
        public bool hasActivePlan;
        public PegaNpcPlanSnapshot activePlan;
        public PegaNpcPlanSelectionResult latestSelection;
        public readonly List<IPegaNpcActionProvider> actionProviders = new List<IPegaNpcActionProvider>();
        public PegaNpcKnowledgeState knowledgeState;
        public readonly List<string> issues = new List<string>();
    }

    /// <summary>Reads existing PEGA snapshots; reflection is confined to this Editor assembly for private retained state.</summary>
    internal static class PegaGoapRuntimeInspectorReader
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo DesireProfileField = typeof(PegaNpcAutonomy).GetField("desireProfile", InstancePrivate);
        private static readonly FieldInfo DesireStateField = typeof(PegaNpcAutonomy).GetField("_desireState", InstancePrivate);
        private static readonly FieldInfo HasActivePlanField = typeof(PegaNpcAutonomy).GetField("_hasActivePlan", InstancePrivate);
        private static readonly FieldInfo ActivePlanField = typeof(PegaNpcAutonomy).GetField("_activePlan", InstancePrivate);
        private static readonly FieldInfo LatestSelectionField = typeof(PegaNpcAutonomy).GetField("_latestSelection", InstancePrivate);
        private static readonly FieldInfo ActionProvidersField = typeof(PegaNpcAutonomy).GetField("_actionProviders", InstancePrivate);
        public static RuntimeView Read(PegaNpcAutonomy autonomy)
        {
            RuntimeView view = new RuntimeView { autonomy = autonomy };
            view.profile = ReadField<PegaNpcDesireProfile>(DesireProfileField, autonomy, "Desire Profile", view.issues);
            PegaNpcDesireState desireState = ReadField<PegaNpcDesireState>(DesireStateField, autonomy, "estado runtime dos Desejos", view.issues);
            if (desireState != null)
            {
                view.desireState = desireState.GetSnapshot();
            }

            view.hasActivePlan = ReadField<bool>(HasActivePlanField, autonomy, "flag do plano ativo", view.issues);
            view.activePlan = ReadField<PegaNpcPlanSnapshot>(ActivePlanField, autonomy, "snapshot do plano ativo", view.issues);
            view.latestSelection = ReadField<PegaNpcPlanSelectionResult>(LatestSelectionField, autonomy, "último resultado de seleção", view.issues);
            ReadActionProviders(autonomy, view);

            view.knowledgeState = autonomy.GetComponent<PegaNpcKnowledgeState>();
            if (view.knowledgeState == null)
            {
                view.issues.Add("Componente PEGANpcKnowledgeState não encontrado no NPC resolvido.");
            }

            return view;
        }

        private static void ReadActionProviders(PegaNpcAutonomy autonomy, RuntimeView view)
        {
            object raw = ReadRawField(ActionProvidersField, autonomy, "lista de Action providers", view.issues);
            if (raw is IEnumerable providers)
            {
                foreach (object item in providers)
                {
                    if (item is IPegaNpcActionProvider provider)
                    {
                        view.actionProviders.Add(provider);
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
