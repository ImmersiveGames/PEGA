using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.NPC;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PEGA.Diagnostics
{
    /// <summary>Verifies Group Knowledge sharing and isolation without Actions or GOAP planning.</summary>
    public sealed class PEGAGroupKnowledgeProofWindow : EditorWindow
    {
        private const string MissionContentScenePath = "Assets/PEGA/Scenes/MissionActivityContent.unity";
        private const string ProofSource = "PEGA Group Knowledge proof";

        private enum ProofEvidenceOrigin
        {
            Verification = 1
        }

        [SerializeField] private PEGAActorGroupKnowledgeState _groupKnowledge;
        [SerializeField] private PEGAGlobalKnowledgeState _globalKnowledge;
        [SerializeField] private ActorGroupAccessBinding _groupAccess;
        [SerializeField] private PEGAActorGroupKnowledgeReader[] _readers = Array.Empty<PEGAActorGroupKnowledgeReader>();

        private string _result;
        private MessageType _resultType = MessageType.Info;

        [MenuItem("PEGA/Diagnostics/Group Knowledge Proof")]
        private static void Open()
        {
            GetWindow<PEGAGroupKnowledgeProofWindow>("Group Knowledge Proof");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PEGA · Actor Group Knowledge Proof", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Inicie um novo Play Mode com a Mission carregada. A prova usa os três larápios e os dois Actor Groups confirmados pelo Framework. Ela não executa Actions nem consulta o planner.",
                MessageType.Info);

            _groupKnowledge = (PEGAActorGroupKnowledgeState)EditorGUILayout.ObjectField(
                "Authority Group", _groupKnowledge, typeof(PEGAActorGroupKnowledgeState), true);
            _globalKnowledge = (PEGAGlobalKnowledgeState)EditorGUILayout.ObjectField(
                "Global Knowledge preservado", _globalKnowledge, typeof(PEGAGlobalKnowledgeState), true);
            _groupAccess = (ActorGroupAccessBinding)EditorGUILayout.ObjectField(
                "Acesso oficial a Groups", _groupAccess, typeof(ActorGroupAccessBinding), true);

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
                SetResult("Entre em Play Mode e aguarde a composição da Activity antes de detectar os componentes.", MessageType.Warning);
                return;
            }

            Scene missionScene = FindLoadedMissionScene();
            if (!missionScene.IsValid())
            {
                SetResult("Abra a cena de conteúdo da Mission e entre em Play Mode.", MessageType.Warning);
                return;
            }

            PEGAActorGroupKnowledgeState[] groupAuthorities = FindInScene<PEGAActorGroupKnowledgeState>(missionScene);
            PEGAGlobalKnowledgeState[] globalAuthorities = FindInScene<PEGAGlobalKnowledgeState>(missionScene);
            ActorGroupAccessBinding[] groupBindings = FindInScene<ActorGroupAccessBinding>(missionScene);
            _readers = FindInScene<PEGAActorGroupKnowledgeReader>(missionScene);

            if (groupAuthorities.Length != 1 || globalAuthorities.Length != 1 ||
                groupBindings.Length != 1 || _readers.Length != 3)
            {
                SetResult(
                    $"Esperados 1 owner Group, 1 autoridade Global, 1 binding oficial e 3 leitores; encontrados {groupAuthorities.Length}, {globalAuthorities.Length}, {groupBindings.Length} e {_readers.Length}.",
                    MessageType.Error);
                return;
            }

            _groupKnowledge = groupAuthorities[0];
            _globalKnowledge = globalAuthorities[0];
            _groupAccess = groupBindings[0];
            SetResult("Componentes encontrados. Execute a prova uma vez neste Play Mode recém-iniciado.", MessageType.Info);
        }

        private void RunProof()
        {
            if (!EditorApplication.isPlaying || _groupKnowledge == null || _globalKnowledge == null ||
                _groupAccess == null || _readers == null || _readers.Length != 3)
            {
                SetResult("Detecte a Mission enquanto ela está em Play Mode antes de executar a prova.", MessageType.Error);
                return;
            }

            if (!_groupKnowledge.IsAvailable || !_globalKnowledge.IsAvailable || !_groupAccess.IsAccessAvailable)
            {
                SetResult("A Activity ainda não disponibilizou as authorities e o binding oficial da Mission.", MessageType.Error);
                return;
            }

            if (!TryFindMissionGroups(out List<GroupMember> firstGroup, out GroupId firstGroupId,
                    out GroupMember secondGroupMember, out GroupId secondGroupId, out string setupIssue))
            {
                SetResult(setupIssue, MessageType.Error);
                return;
            }

            var key = PEGAGlobalKnowledgeFacts.MainObjectiveIdentified.For((byte)0);
            double observationTime = Time.timeAsDouble + 0.01d;

            if (!AreAuthorizedUnknown(firstGroup[0].Reader.ReadGroupFact(firstGroupId, key, observationTime)) ||
                !AreAuthorizedUnknown(firstGroup[1].Reader.ReadGroupFact(firstGroupId, key, observationTime)) ||
                !AreAuthorizedUnknown(secondGroupMember.Reader.ReadGroupFact(secondGroupId, key, observationTime)))
            {
                SetResult("As stores não estão vazias. Reinicie o Play Mode para repetir a prova sem estado anterior.", MessageType.Error);
                return;
            }

            if (!IsDeniedAndRedacted(
                    secondGroupMember.Reader.ReadGroupFact(firstGroupId, key, observationTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember) ||
                !IsDeniedAndRedacted(
                    firstGroup[0].Reader.ReadGroupFact(secondGroupId, key, observationTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember) ||
                !IsDeniedAndRedacted(
                    firstGroup[1].Reader.ReadGroupFact(secondGroupId, key, observationTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember))
            {
                SetResult("A consulta anterior à gravação não distinguiu corretamente ausência de conhecimento e falta de autorização.", MessageType.Error);
                return;
            }

            PEGAKnowledgeEvidence firstInitialEvidence = CreateEvidence(observationTime, "first-group:false");
            PEGAActorGroupKnowledgeAccessStatus firstWriteAccess = _groupKnowledge.ObserveFact(
                firstGroupId,
                key,
                false,
                firstInitialEvidence,
                out PEGAKnowledgeWriteResult firstWrite);
            if (firstWriteAccess != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                firstWrite != PEGAKnowledgeWriteResult.Recorded)
            {
                SetResult("A autoridade PEGA não registrou o fato inicial do primeiro Group.", MessageType.Error);
                return;
            }

            double secondGroupTime = observationTime + 0.001d;
            PEGAKnowledgeEvidence secondGroupEvidence = CreateEvidence(secondGroupTime, "second-group:true");
            PEGAActorGroupKnowledgeAccessStatus secondWriteAccess = _groupKnowledge.ObserveFact(
                secondGroupId,
                key,
                true,
                secondGroupEvidence,
                out PEGAKnowledgeWriteResult secondWrite);
            if (secondWriteAccess != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                secondWrite != PEGAKnowledgeWriteResult.Recorded)
            {
                SetResult("A autoridade PEGA não registrou o fato do segundo Group.", MessageType.Error);
                return;
            }

            double groupQueryTime = secondGroupTime;
            PEGAActorGroupKnowledgeReadResult<bool> firstMemberRead =
                firstGroup[0].Reader.ReadGroupFact(firstGroupId, key, groupQueryTime);
            PEGAActorGroupKnowledgeReadResult<bool> secondMemberRead =
                firstGroup[1].Reader.ReadGroupFact(firstGroupId, key, groupQueryTime);
            PEGAActorGroupKnowledgeReadResult<bool> otherGroupRead =
                secondGroupMember.Reader.ReadGroupFact(secondGroupId, key, groupQueryTime);
            if (firstMemberRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                secondMemberRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                otherGroupRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                !AreEquivalentKnownFacts(firstMemberRead.Fact, secondMemberRead.Fact, false) ||
                !HasExpectedEvidence(firstMemberRead.Fact, firstInitialEvidence) ||
                !IsExpectedKnownFact(otherGroupRead.Fact, true, secondGroupEvidence))
            {
                SetResult("Os membros não observaram fatos conhecidos distintos com a mesma metadata ou os stores dos Groups não permaneceram independentes.", MessageType.Error);
                return;
            }

            if (!IsDeniedAndRedacted(
                    secondGroupMember.Reader.ReadGroupFact(firstGroupId, key, groupQueryTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember) ||
                !IsDeniedAndRedacted(
                    firstGroup[0].Reader.ReadGroupFact(secondGroupId, key, groupQueryTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember) ||
                !IsDeniedAndRedacted(
                    firstGroup[1].Reader.ReadGroupFact(secondGroupId, key, groupQueryTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember))
            {
                SetResult("Uma consulta cruzada não membro deixou de ser negada e redigida.", MessageType.Error);
                return;
            }

            if (!VerifyLeaveRevokesAccess(firstGroup[0], firstGroupId, key, groupQueryTime, out string membershipIssue))
            {
                SetResult(membershipIssue, MessageType.Error);
                return;
            }

            if (!VerifyGlobalRemainsReadable(key, out PEGAKnowledgeFact<bool> globalFact, out string globalIssue))
            {
                SetResult(globalIssue, MessageType.Error);
                return;
            }

            PEGAActorGroupKnowledgeReadResult<bool> finalFirstMemberRead =
                firstGroup[0].Reader.ReadGroupFact(firstGroupId, key, groupQueryTime);
            PEGAActorGroupKnowledgeReadResult<bool> finalSecondMemberRead =
                firstGroup[1].Reader.ReadGroupFact(firstGroupId, key, groupQueryTime);
            PEGAActorGroupKnowledgeReadResult<bool> finalOtherGroupRead =
                secondGroupMember.Reader.ReadGroupFact(secondGroupId, key, groupQueryTime);
            if (finalFirstMemberRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                finalSecondMemberRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                finalOtherGroupRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                !AreEquivalentKnownFacts(finalFirstMemberRead.Fact, finalSecondMemberRead.Fact, false) ||
                !HasExpectedEvidence(finalFirstMemberRead.Fact, firstInitialEvidence) ||
                !IsExpectedKnownFact(finalOtherGroupRead.Fact, true, secondGroupEvidence))
            {
                SetResult("A gravação Global alterou ou tornou indisponível algum fato Group.", MessageType.Error);
                return;
            }

            if (!IndividualKnowledgeRemainsIndependent(_readers, key, groupQueryTime))
            {
                SetResult("A prova encontrou uma cópia de Group Knowledge no armazenamento Individual.", MessageType.Error);
                return;
            }

            SetResult(
                $"PASS: {_readers.Length} Actors; Group {firstGroupId.StableText} compartilhado: {DescribeAuthorizedFact(finalFirstMemberRead.Fact)}; Group {secondGroupId.StableText}: {DescribeAuthorizedFact(finalOtherGroupRead.Fact)}. Consultas cruzadas retornaram NotMember com Unknown vazio; Leave revogou o acesso e Join o restaurou. Global compartilhado pelos três consumidores GOAP: {DescribeAuthorizedFact(globalFact)}. Stores Individuais permaneceram independentes; nenhuma Action ou decisão GOAP foi executada.",
                MessageType.Info);
        }

        private bool VerifyGlobalRemainsReadable(
            PEGAKnowledgeFactKey<byte, bool> key,
            out PEGAKnowledgeFact<bool> globalFact,
            out string issue)
        {
            globalFact = default;
            var globalReaders = new IPEGAKnowledgeFactReader[_readers.Length];
            for (int index = 0; index < _readers.Length; index++)
            {
                PEGANpcAutonomy autonomy = _readers[index].GetComponent<PEGANpcAutonomy>();
                if (autonomy == null ||
                    !TryGetGlobalReader(autonomy.GoapKnowledgeState, out IPEGAKnowledgeFactReader reader) ||
                    !ReferenceEquals(reader, _globalKnowledge))
                {
                    issue = "Um dos consumidores GOAP não compõe a autoridade Global compartilhada existente.";
                    return false;
                }

                globalReaders[index] = reader;
            }

            PEGAKnowledgeFact<bool> existing = _globalKnowledge.ReadFact(key, Time.timeAsDouble);
            double initialTime = existing.HasObservationTime
                ? Math.Max(Time.timeAsDouble, existing.ObservationTimeSeconds + 0.001d)
                : Time.timeAsDouble;
            double updateTime = initialTime + 0.001d;
            PEGAKnowledgeEvidence initialEvidence = CreateEvidence(initialTime, "global:false");
            PEGAKnowledgeWriteResult initialWrite = _globalKnowledge.ObserveFact(key, false, initialEvidence);
            PEGAKnowledgeFact<bool>[] initialFacts = ReadGlobalFacts(globalReaders, key, initialTime);
            if ((initialWrite != PEGAKnowledgeWriteResult.Recorded && initialWrite != PEGAKnowledgeWriteResult.Replaced) ||
                !AllEquivalentGlobalFacts(initialFacts, false) ||
                !HasExpectedEvidence(initialFacts[0], initialEvidence))
            {
                issue = "Os três consumidores GOAP não consultaram a mesma observação Global inicial.";
                return false;
            }

            PEGAKnowledgeEvidence updatedEvidence = CreateEvidence(updateTime, "global:true");
            PEGAKnowledgeWriteResult updateWrite = _globalKnowledge.ObserveFact(key, true, updatedEvidence);
            PEGAKnowledgeFact<bool>[] updatedFacts = ReadGlobalFacts(globalReaders, key, updateTime);
            if (updateWrite != PEGAKnowledgeWriteResult.Replaced ||
                !AllEquivalentGlobalFacts(updatedFacts, true) ||
                !HasExpectedEvidence(updatedFacts[0], updatedEvidence))
            {
                issue = "Os três consumidores GOAP não consultaram a atualização da mesma authority Global.";
                return false;
            }

            globalFact = updatedFacts[0];
            issue = string.Empty;
            return true;
        }

        private static PEGAKnowledgeFact<bool>[] ReadGlobalFacts(
            IReadOnlyList<IPEGAKnowledgeFactReader> readers,
            PEGAKnowledgeFactKey<byte, bool> key,
            double queryTime)
        {
            var facts = new PEGAKnowledgeFact<bool>[readers.Count];
            for (int index = 0; index < readers.Count; index++)
            {
                facts[index] = readers[index].ReadFact(key, queryTime);
            }

            return facts;
        }

        private static bool AllEquivalentGlobalFacts(PEGAKnowledgeFact<bool>[] facts, bool expectedValue)
        {
            if (facts == null || facts.Length == 0 || !IsExpectedGlobalFact(facts[0], expectedValue))
            {
                return false;
            }

            for (int index = 1; index < facts.Length; index++)
            {
                if (!IsExpectedGlobalFact(facts[index], expectedValue) ||
                    facts[index].ObservationTimeSeconds != facts[0].ObservationTimeSeconds ||
                    !HasSameProvenance(facts[index], facts[0]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExpectedGlobalFact(PEGAKnowledgeFact<bool> fact, bool expectedValue) =>
            fact.Status == PEGAKnowledgeStatus.Known &&
            fact.Scope == PEGAKnowledgeScope.Global &&
            fact.HasObservationTime &&
            fact.TryGetValue(out bool value) && value == expectedValue;

        private static string DescribeAuthorizedFact(PEGAKnowledgeFact<bool> fact)
        {
            fact.TryGetValue(out bool value);
            var evidence = new List<string>(fact.Evidence.Count);
            for (int index = 0; index < fact.Evidence.Count; index++)
            {
                PEGAKnowledgeEvidence item = fact.Evidence[index];
                evidence.Add(
                    $"origin={item.Origin}, time={item.ObservationTimeSeconds:0.000}, source={item.Source}, payload={item.Payload}");
            }

            return $"value={value}, status={fact.Status}, scope={fact.Scope}, observationTime={fact.ObservationTimeSeconds:0.000}, evidence=[{string.Join("; ", evidence)}]";
        }

        private static bool TryGetGlobalReader(
            IGoapKnowledgeStateView view,
            out IPEGAKnowledgeFactReader reader)
        {
            if (view != null && view.Source == GoapStateSource.KnowledgeState)
            {
                IReadOnlyList<IGoapKnowledgeCapability> capabilities = view.Capabilities;
                for (int index = 0; index < capabilities.Count; index++)
                {
                    if (capabilities[index] is IPEGAKnowledgeFactReader candidate &&
                        candidate.Scope == PEGAKnowledgeScope.Global && candidate.IsAvailable)
                    {
                        reader = candidate;
                        return true;
                    }
                }
            }

            reader = null;
            return false;
        }

        private bool TryFindMissionGroups(
            out List<GroupMember> firstGroup,
            out GroupId firstGroupId,
            out GroupMember secondGroupMember,
            out GroupId secondGroupId,
            out string issue)
        {
            firstGroup = null;
            firstGroupId = default;
            secondGroupMember = default;
            secondGroupId = default;
            var membersByGroup = new Dictionary<GroupId, List<GroupMember>>();

            for (int index = 0; index < _readers.Length; index++)
            {
                PEGAActorGroupKnowledgeReader reader = _readers[index];
                ActorDeclaration actor = reader != null ? reader.GetComponent<ActorDeclaration>() : null;
                if (!TryReadActorId(actor, out ActorId actorId) ||
                    !_groupAccess.TryGetGroupsForActor(actorId, 16, out IReadOnlyList<GroupId> groups, out issue) ||
                    groups.Count != 1)
                {
                    issue = "Cada larápio deve ter um ActorId válido e uma membership vigente no contrato oficial.";
                    return false;
                }

                GroupId groupId = groups[0];
                if (!_groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot group, out issue) ||
                    !group.IsValid ||
                    !_groupAccess.TryIsMember(groupId, actorId, out bool isMember, out issue) || !isMember)
                {
                    issue = "O Framework não confirmou um Group vivo e a membership do Actor.";
                    return false;
                }

                if (!membersByGroup.TryGetValue(groupId, out List<GroupMember> groupMembers))
                {
                    groupMembers = new List<GroupMember>();
                    membersByGroup.Add(groupId, groupMembers);
                }

                groupMembers.Add(new GroupMember(reader, actorId, groupId));
            }

            foreach (KeyValuePair<GroupId, List<GroupMember>> entry in membersByGroup)
            {
                if (entry.Value.Count == 2)
                {
                    firstGroupId = entry.Key;
                    firstGroup = entry.Value;
                }
                else if (entry.Value.Count == 1)
                {
                    secondGroupId = entry.Key;
                    secondGroupMember = entry.Value[0];
                }
                else
                {
                    issue = "A Mission precisa conter exatamente dois membros no primeiro Group e um no segundo.";
                    return false;
                }
            }

            if (membersByGroup.Count != 2 || firstGroup == null || secondGroupMember.Reader == null ||
                firstGroupId == secondGroupId)
            {
                issue = "A API oficial do Framework não confirmou dois Groups com distribuição de membros 2 + 1.";
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private bool VerifyLeaveRevokesAccess(
            GroupMember member,
            GroupId groupId,
            PEGAKnowledgeFactKey<byte, bool> key,
            double queryTime,
            out string issue)
        {
            if (!_groupAccess.TryLeaveGroup(groupId, member.ActorId, out issue))
            {
                issue = "O Framework não aceitou o Leave temporário usado na prova: " + issue;
                return false;
            }

            bool deniedAndRedacted = false;
            bool rejoined = false;
            try
            {
                deniedAndRedacted = IsDeniedAndRedacted(
                    member.Reader.ReadGroupFact(groupId, key, queryTime),
                    PEGAActorGroupKnowledgeAccessStatus.NotMember);
            }
            finally
            {
                rejoined = _groupAccess.TryJoinGroup(groupId, member.ActorId, out issue);
            }

            if (!rejoined)
            {
                issue = "A prova não conseguiu restaurar a membership original pelo Framework: " + issue;
                return false;
            }

            PEGAActorGroupKnowledgeReadResult<bool> restoredRead = member.Reader.ReadGroupFact(groupId, key, queryTime);
            if (!deniedAndRedacted ||
                restoredRead.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized ||
                !IsExpectedKnownFact(restoredRead.Fact, false, null))
            {
                issue = "Leave não revogou o acesso ou Join não o restaurou corretamente.";
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private static PEGAKnowledgeEvidence CreateEvidence(double time, string payload) =>
            PEGAKnowledgeEvidence.From(ProofEvidenceOrigin.Verification, time, ProofSource, payload);

        private static bool AreAuthorizedUnknown(PEGAActorGroupKnowledgeReadResult<bool> result) =>
            result.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.Authorized &&
            result.Fact.Status == PEGAKnowledgeStatus.Unknown &&
            result.Fact.Scope == PEGAKnowledgeScope.Group &&
            !result.Fact.HasObservationTime &&
            result.Fact.Evidence.Count == 0 &&
            !result.Fact.TryGetValue(out _);

        private static bool IsDeniedAndRedacted(
            PEGAActorGroupKnowledgeReadResult<bool> result,
            PEGAActorGroupKnowledgeAccessStatus expectedStatus) =>
            !result.IsAuthorized &&
            result.AccessStatus == expectedStatus &&
            result.Fact.Status == PEGAKnowledgeStatus.Unknown &&
            result.Fact.Scope == PEGAKnowledgeScope.Group &&
            !result.Fact.HasObservationTime &&
            result.Fact.Evidence.Count == 0 &&
            !result.Fact.TryGetValue(out _);

        private static bool AreEquivalentKnownFacts(
            PEGAKnowledgeFact<bool> first,
            PEGAKnowledgeFact<bool> second,
            bool expectedValue) =>
            IsExpectedKnownFact(first, expectedValue, null) &&
            IsExpectedKnownFact(second, expectedValue, null) &&
            first.HasObservationTime && second.HasObservationTime &&
            first.ObservationTimeSeconds == second.ObservationTimeSeconds &&
            HasSameProvenance(first, second);

        private static bool IsExpectedKnownFact(
            PEGAKnowledgeFact<bool> fact,
            bool expectedValue,
            PEGAKnowledgeEvidence? expectedEvidence) =>
            fact.Status == PEGAKnowledgeStatus.Known &&
            fact.Scope == PEGAKnowledgeScope.Group &&
            fact.TryGetValue(out bool value) && value == expectedValue &&
            (!expectedEvidence.HasValue || HasExpectedEvidence(fact, expectedEvidence.Value));

        private static bool HasExpectedEvidence(PEGAKnowledgeFact<bool> fact, PEGAKnowledgeEvidence expected)
        {
            if (fact.Evidence.Count != 1)
            {
                return false;
            }

            PEGAKnowledgeEvidence actual = fact.Evidence[0];
            return actual.Origin.Equals(expected.Origin) &&
                actual.ObservationTimeSeconds == expected.ObservationTimeSeconds &&
                Equals(actual.Source, expected.Source) && Equals(actual.Payload, expected.Payload);
        }

        private static bool HasSameProvenance(PEGAKnowledgeFact<bool> first, PEGAKnowledgeFact<bool> second)
        {
            if (first.Evidence.Count != second.Evidence.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Evidence.Count; index++)
            {
                PEGAKnowledgeEvidence left = first.Evidence[index];
                PEGAKnowledgeEvidence right = second.Evidence[index];
                if (!left.Origin.Equals(right.Origin) ||
                    left.ObservationTimeSeconds != right.ObservationTimeSeconds ||
                    !Equals(left.Source, right.Source) || !Equals(left.Payload, right.Payload))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IndividualKnowledgeRemainsIndependent(
            IReadOnlyList<PEGAActorGroupKnowledgeReader> readers,
            PEGAKnowledgeFactKey<byte, bool> key,
            double queryTime)
        {
            for (int index = 0; index < readers.Count; index++)
            {
                PEGANpcKnowledgeState individual = readers[index].GetComponent<PEGANpcKnowledgeState>();
                if (individual == null)
                {
                    return false;
                }

                PEGAKnowledgeFact<bool> fact = individual.ReadFact(key, queryTime);
                if (fact.Status != PEGAKnowledgeStatus.Unknown || fact.Scope != PEGAKnowledgeScope.Individual)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadActorId(ActorDeclaration actor, out ActorId actorId)
        {
            actorId = default;
            if (actor == null)
            {
                return false;
            }

            try
            {
                actorId = actor.ActorId;
                return actorId.IsValid;
            }
            catch (ArgumentException)
            {
                return false;
            }
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

        private readonly struct GroupMember
        {
            internal GroupMember(
                PEGAActorGroupKnowledgeReader reader,
                ActorId actorId,
                GroupId groupId)
            {
                Reader = reader;
                ActorId = actorId;
                GroupId = groupId;
            }

            internal PEGAActorGroupKnowledgeReader Reader { get; }

            internal ActorId ActorId { get; }

            internal GroupId GroupId { get; }
        }
    }
}
