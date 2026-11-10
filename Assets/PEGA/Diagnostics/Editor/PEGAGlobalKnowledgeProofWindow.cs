using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.GOAP;
using PEGA.NPC.Autonomy;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Global;
using PEGA.NPC.Knowledge.Individual;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PEGA.Diagnostics
{
    /// <summary>Runs a small, explicit Global Knowledge write/read proof without invoking Actions or planning.</summary>
    public sealed class PegaGlobalKnowledgeProofWindow : EditorWindow
    {
        private const string MissionContentScenePath = "Assets/PEGA/Scenes/MissionActivityContent.unity";
        private const string ProofSource = "PEGA Global Knowledge proof";

        private enum ProofEvidenceOrigin
        {
            Verification = 1
        }

        [SerializeField] private PegaGlobalKnowledgeState globalAuthority;
        [SerializeField] private PegaNpcAutonomy firstConsumer;
        [SerializeField] private PegaNpcAutonomy secondConsumer;
        [SerializeField] private ActorGroupAccessBinding groupAccess;

        private GroupId _firstGroup;
        private GroupId _secondGroup;
        private string _result;
        private MessageType _resultType = MessageType.Info;

        [MenuItem("PEGA/Diagnostics/Global Knowledge Proof")]
        private static void Open()
        {
            GetWindow<PegaGlobalKnowledgeProofWindow>("Global Knowledge Proof");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PEGA · Global Knowledge Proof", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "No Play Mode, a prova registra e atualiza um fato Global, consulta as vistas GOAP de dois NPCs membros de Groups distintos e uma vista GOAP independente. Ela não executa Actions nem solicita decisões ao planner.",
                MessageType.Info);

            globalAuthority = (PegaGlobalKnowledgeState)EditorGUILayout.ObjectField(
                "Autoridade Global", globalAuthority, typeof(PegaGlobalKnowledgeState), true);
            firstConsumer = (PegaNpcAutonomy)EditorGUILayout.ObjectField(
                "Primeiro consumidor", firstConsumer, typeof(PegaNpcAutonomy), true);
            secondConsumer = (PegaNpcAutonomy)EditorGUILayout.ObjectField(
                "Segundo consumidor", secondConsumer, typeof(PegaNpcAutonomy), true);
            groupAccess = (ActorGroupAccessBinding)EditorGUILayout.ObjectField(
                "Acesso oficial a Groups", groupAccess, typeof(ActorGroupAccessBinding), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Detectar Mission carregada"))
                {
                    ResolveMissionSetup();
                }

                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Executar prova"))
                    {
                        RunProof();
                    }
                }
            }

            EditorGUILayout.LabelField("Unity Play Mode", EditorApplication.isPlaying ? "Ativo" : "Inativo");
            if (!string.IsNullOrEmpty(_result))
            {
                EditorGUILayout.HelpBox(_result, _resultType);
            }
        }

        private void ResolveMissionSetup()
        {
            if (!EditorApplication.isPlaying)
            {
                SetResult("Entre no Play Mode e aguarde a composição da Activity antes de detectar os consumidores.", MessageType.Warning);
                return;
            }

            Scene missionScene = FindLoadedMissionScene();
            if (!missionScene.IsValid())
            {
                SetResult("Abra a cena de conteúdo da Mission e entre no Play Mode para compor a Activity.", MessageType.Warning);
                return;
            }

            PegaGlobalKnowledgeState[] authorities = FindInScene<PegaGlobalKnowledgeState>(missionScene);
            ActorGroupAccessBinding[] groupBindings = FindInScene<ActorGroupAccessBinding>(missionScene);
            PegaNpcAutonomy[] autonomies = FindInScene<PegaNpcAutonomy>(missionScene);
            if (authorities.Length != 1)
            {
                SetResult($"Esperada uma autoridade Global na Mission; encontradas: {authorities.Length}.", MessageType.Error);
                return;
            }

            globalAuthority = authorities[0];
            groupAccess = null;
            firstConsumer = null;
            secondConsumer = null;
            _firstGroup = default;
            _secondGroup = default;

            for (int bindingIndex = 0; bindingIndex < groupBindings.Length; bindingIndex++)
            {
                ActorGroupAccessBinding binding = groupBindings[bindingIndex];
                if (TryFindConsumersInDistinctGroups(
                        binding,
                        autonomies,
                        out PegaNpcAutonomy first,
                        out GroupId firstGroup,
                        out PegaNpcAutonomy second,
                        out GroupId secondGroup))
                {
                    groupAccess = binding;
                    firstConsumer = first;
                    _firstGroup = firstGroup;
                    secondConsumer = second;
                    _secondGroup = secondGroup;
                    SetResult(
                        $"Consumidores detectados em Groups distintos: {_firstGroup.StableText} e {_secondGroup.StableText}.",
                        MessageType.Info);
                    return;
                }
            }

            SetResult("Não foi possível confirmar dois Actors em Groups distintos pela API oficial do Framework.", MessageType.Error);
        }

        private void RunProof()
        {
            if (!EditorApplication.isPlaying)
            {
                SetResult("A prova exige Play Mode para que a Activity tenha ativado seu owner Global.", MessageType.Warning);
                return;
            }

            if (globalAuthority == null || !globalAuthority.IsAvailable ||
                firstConsumer == null || secondConsumer == null || groupAccess == null)
            {
                SetResult("Detecte a Mission carregada e aguarde a entrada da Activity antes de executar a prova.", MessageType.Warning);
                return;
            }

            string firstIssue = string.Empty;
            string secondIssue = string.Empty;
            bool firstMembershipConfirmed = TryConfirmMembership(
                firstConsumer,
                _firstGroup,
                out ActorId firstActor,
                out firstIssue);
            bool secondMembershipConfirmed = TryConfirmMembership(
                secondConsumer,
                _secondGroup,
                out ActorId secondActor,
                out secondIssue);
            if (!firstMembershipConfirmed || !secondMembershipConfirmed)
            {
                SetResult($"Membership não confirmada: {firstIssue} {secondIssue}".Trim(), MessageType.Error);
                return;
            }

            if (_firstGroup == _secondGroup || firstActor == secondActor)
            {
                SetResult("A prova requer dois Actors diferentes em GroupId distintos.", MessageType.Error);
                return;
            }

            bool firstReaderAvailable = TryGetGlobalReader(
                firstConsumer.GoapKnowledgeState,
                out IPegaKnowledgeFactReader firstReader);
            bool secondReaderAvailable = TryGetGlobalReader(
                secondConsumer.GoapKnowledgeState,
                out IPegaKnowledgeFactReader secondReader);
            if (!firstReaderAvailable || !secondReaderAvailable ||
                !ReferenceEquals(firstReader, globalAuthority) || !ReferenceEquals(secondReader, globalAuthority))
            {
                SetResult("Os dois consumidores GOAP não compõem a mesma autoridade Global explicitamente.", MessageType.Error);
                return;
            }

            var independentView = new PegaKnowledgeGoapStateView(globalAuthority);
            bool independentReaderAvailable = TryGetGlobalReader(
                independentView,
                out IPegaKnowledgeFactReader independentReader);
            if (!independentReaderAvailable || !ReferenceEquals(independentReader, globalAuthority))
            {
                SetResult("A composição genérica de GOAP não expôs a capability Global independente.", MessageType.Error);
                return;
            }

            PegaKnowledgeFactKey<byte, bool> key = PegaGlobalKnowledgeFacts.MainObjectiveIdentified.For(0);
            PegaKnowledgeFact<bool> existing = globalAuthority.ReadFact(key, Time.timeAsDouble);
            double firstTime = existing.HasObservationTime
                ? Math.Max(Time.timeAsDouble, existing.ObservationTimeSeconds + 0.001d)
                : Time.timeAsDouble;
            double updatedTime = firstTime + 0.001d;

            PegaKnowledgeEvidence initialEvidence = PegaKnowledgeEvidence.From(
                ProofEvidenceOrigin.Verification,
                firstTime,
                ProofSource,
                "registered:false");
            PegaKnowledgeWriteResult initialWrite = globalAuthority.ObserveFact(key, false, initialEvidence);
            PegaKnowledgeFact<bool> initialFirst = firstReader.ReadFact(key, firstTime);
            PegaKnowledgeFact<bool> initialSecond = secondReader.ReadFact(key, firstTime);
            PegaKnowledgeFact<bool> initialIndependent = independentReader.ReadFact(key, firstTime);
            if ((initialWrite != PegaKnowledgeWriteResult.Recorded && initialWrite != PegaKnowledgeWriteResult.Replaced) ||
                !AreEquivalentKnownFacts(initialFirst, initialSecond, false) ||
                !AreEquivalentKnownFacts(initialFirst, initialIndependent, false) ||
                !HasProofProvenance(initialFirst, initialEvidence))
            {
                SetResult("A primeira gravação não foi observada igualmente pelos três consumidores.", MessageType.Error);
                return;
            }

            PegaKnowledgeEvidence updatedEvidence = PegaKnowledgeEvidence.From(
                ProofEvidenceOrigin.Verification,
                updatedTime,
                ProofSource,
                "updated:true");
            PegaKnowledgeWriteResult updateWrite = globalAuthority.ObserveFact(key, true, updatedEvidence);
            PegaKnowledgeFact<bool> updatedFirst = firstReader.ReadFact(key, updatedTime);
            PegaKnowledgeFact<bool> updatedSecond = secondReader.ReadFact(key, updatedTime);
            PegaKnowledgeFact<bool> updatedIndependent = independentReader.ReadFact(key, updatedTime);
            if (updateWrite != PegaKnowledgeWriteResult.Replaced ||
                !AreEquivalentKnownFacts(updatedFirst, updatedSecond, true) ||
                !AreEquivalentKnownFacts(updatedFirst, updatedIndependent, true) ||
                !HasProofProvenance(updatedFirst, updatedEvidence))
            {
                SetResult("A atualização Global não foi observada igualmente pelos três consumidores.", MessageType.Error);
                return;
            }

            PegaNpcKnowledgeState individualAuthority = firstConsumer.GetComponent<PegaNpcKnowledgeState>();
            if (individualAuthority == null)
            {
                SetResult("O consumidor não tem a autoridade Individual esperada.", MessageType.Error);
                return;
            }

            PegaKnowledgeFact<bool> individualFirst = individualAuthority.ReadFact(key, updatedTime);
            if (individualFirst.Status != PegaKnowledgeStatus.Unknown ||
                individualFirst.Scope != PegaKnowledgeScope.Individual)
            {
                SetResult("A prova encontrou uma cópia do fato Global no Knowledge Individual.", MessageType.Error);
                return;
            }

            SetResult(
                $"PASS: Group {_firstGroup.StableText} / Actor {firstActor.StableText} e Group {_secondGroup.StableText} / Actor {secondActor.StableText}; escrita {initialWrite}, atualização {updateWrite}; três vistas leram Known=true, Scope=Global, origem={ProofEvidenceOrigin.Verification}, source='{ProofSource}', payload='updated:true', timestamp={updatedTime:0.000}; Knowledge Individual permaneceu Unknown.",
                MessageType.Info);
        }

        private bool TryConfirmMembership(PegaNpcAutonomy autonomy, GroupId groupId, out ActorId actorId, out string issue)
        {
            ActorDeclaration declaration = autonomy != null ? autonomy.GetComponent<ActorDeclaration>() : null;
            if (declaration == null)
            {
                actorId = default;
                issue = "ActorDeclaration ausente.";
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
            }
            catch (ArgumentException exception)
            {
                actorId = default;
                issue = exception.Message;
                return false;
            }

            if (!groupAccess.TryIsMember(groupId, actorId, out bool isMember, out issue) || !isMember ||
                !groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot group, out issue) || !group.IsValid)
            {
                issue = string.IsNullOrEmpty(issue) ? "O Framework não confirmou Group ativo e membership." : issue;
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private static bool TryFindConsumersInDistinctGroups(
            ActorGroupAccessBinding binding,
            PegaNpcAutonomy[] autonomies,
            out PegaNpcAutonomy first,
            out GroupId firstGroup,
            out PegaNpcAutonomy second,
            out GroupId secondGroup)
        {
            first = null;
            second = null;
            firstGroup = default;
            secondGroup = default;
            if (binding == null || !binding.IsAccessAvailable)
            {
                return false;
            }

            var memberships = new List<(PegaNpcAutonomy Autonomy, ActorId Actor, GroupId Group)>();
            for (int autonomyIndex = 0; autonomyIndex < autonomies.Length; autonomyIndex++)
            {
                PegaNpcAutonomy autonomy = autonomies[autonomyIndex];
                ActorDeclaration declaration = autonomy != null ? autonomy.GetComponent<ActorDeclaration>() : null;
                if (declaration == null)
                {
                    continue;
                }

                ActorId actorId;
                try
                {
                    actorId = declaration.ActorId;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!binding.TryGetGroupsForActor(actorId, 64, out IReadOnlyList<GroupId> groups, out _))
                {
                    continue;
                }

                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    GroupId groupId = groups[groupIndex];
                    if (binding.TryIsMember(groupId, actorId, out bool isMember, out _) && isMember)
                    {
                        memberships.Add((autonomy, actorId, groupId));
                    }
                }
            }

            for (int firstIndex = 0; firstIndex < memberships.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < memberships.Count; secondIndex++)
                {
                    if (memberships[firstIndex].Actor == memberships[secondIndex].Actor ||
                        memberships[firstIndex].Group == memberships[secondIndex].Group)
                    {
                        continue;
                    }

                    first = memberships[firstIndex].Autonomy;
                    firstGroup = memberships[firstIndex].Group;
                    second = memberships[secondIndex].Autonomy;
                    secondGroup = memberships[secondIndex].Group;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetGlobalReader(
            IGoapKnowledgeStateView view,
            out IPegaKnowledgeFactReader reader)
        {
            if (view != null && view.Source == GoapStateSource.KnowledgeState)
            {
                IReadOnlyList<IGoapKnowledgeCapability> capabilities = view.Capabilities;
                for (int index = 0; index < capabilities.Count; index++)
                {
                    if (capabilities[index] is IPegaKnowledgeFactReader candidate &&
                        candidate.Scope == PegaKnowledgeScope.Global && candidate.IsAvailable)
                    {
                        reader = candidate;
                        return true;
                    }
                }
            }

            reader = null;
            return false;
        }

        private static bool AreEquivalentKnownFacts(
            PegaKnowledgeFact<bool> left,
            PegaKnowledgeFact<bool> right,
            bool expectedValue)
        {
            return left.Status == PegaKnowledgeStatus.Known &&
                   right.Status == PegaKnowledgeStatus.Known &&
                   left.Scope == PegaKnowledgeScope.Global &&
                   right.Scope == PegaKnowledgeScope.Global &&
                   left.TryGetValue(out bool leftValue) && leftValue == expectedValue &&
                   right.TryGetValue(out bool rightValue) && rightValue == expectedValue &&
                   left.HasObservationTime && right.HasObservationTime &&
                   left.ObservationTimeSeconds == right.ObservationTimeSeconds &&
                   HasSameProvenance(left, right);
        }

        private static bool HasSameProvenance(PegaKnowledgeFact<bool> left, PegaKnowledgeFact<bool> right)
        {
            if (left.Evidence.Count != right.Evidence.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Evidence.Count; index++)
            {
                PegaKnowledgeEvidence leftEvidence = left.Evidence[index];
                PegaKnowledgeEvidence rightEvidence = right.Evidence[index];
                if (!leftEvidence.Origin.Equals(rightEvidence.Origin) ||
                    leftEvidence.ObservationTimeSeconds != rightEvidence.ObservationTimeSeconds ||
                    !Equals(leftEvidence.Source, rightEvidence.Source) ||
                    !Equals(leftEvidence.Payload, rightEvidence.Payload))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasProofProvenance(
            PegaKnowledgeFact<bool> fact,
            PegaKnowledgeEvidence expected)
        {
            if (fact.Evidence.Count != 1)
            {
                return false;
            }

            PegaKnowledgeEvidence actual = fact.Evidence[0];
            return actual.Origin.Equals(expected.Origin) &&
                   actual.ObservationTimeSeconds == expected.ObservationTimeSeconds &&
                   Equals(actual.Source, expected.Source) && Equals(actual.Payload, expected.Payload);
        }

        private static Scene FindLoadedMissionScene()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.isLoaded && string.Equals(scene.path, MissionContentScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            return default;
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            var result = new List<T>();
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].gameObject.scene == scene)
                {
                    result.Add(all[index]);
                }
            }

            return result.ToArray();
        }

        private void SetResult(string result, MessageType messageType)
        {
            _result = result;
            _resultType = messageType;
            Repaint();
        }
    }
}
