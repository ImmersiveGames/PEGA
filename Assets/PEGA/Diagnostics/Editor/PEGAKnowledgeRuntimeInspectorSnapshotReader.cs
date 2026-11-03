using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.NPC;
using UnityEngine;

namespace PEGA.Diagnostics.Editor
{
    internal sealed class PEGAKnowledgeRuntimeInspectorSnapshot
    {
        public double QueryTimeSeconds;
        public bool HasRegisteredFacts;
        public bool HasReadableAuthority;
        public bool HasEnumerationIssues;
        public bool IndividualAvailable;
        public string IndividualAuthorityStatus;
        public readonly List<PEGAKnowledgeRuntimeInspectorFact> IndividualFacts =
            new List<PEGAKnowledgeRuntimeInspectorFact>();
        public bool GlobalAvailable;
        public string GlobalAuthorityStatus;
        public readonly List<PEGAKnowledgeRuntimeInspectorFact> GlobalFacts =
            new List<PEGAKnowledgeRuntimeInspectorFact>();
        public string GroupReaderStatus;
        public readonly List<PEGAKnowledgeRuntimeInspectorGroup> Groups =
            new List<PEGAKnowledgeRuntimeInspectorGroup>();
    }

    internal sealed class PEGAKnowledgeRuntimeInspectorGroup
    {
        public GroupId GroupId;
        public string DisplayName;
        public bool? IsCurrentMember;
        public bool CanRead;
        public PEGAActorGroupKnowledgeAccessStatus AccessStatus;
        public string Diagnostic;
        public readonly List<PEGAKnowledgeRuntimeInspectorFact> Facts =
            new List<PEGAKnowledgeRuntimeInspectorFact>();
    }

    internal sealed class PEGAKnowledgeRuntimeInspectorFact
    {
        public PEGAKnowledgeScope Scope;
        public IPEGAKnowledgeFactKey Key;
        public string DefinitionId;
        public string DefinitionName;
        public string AddressTypeName;
        public string AddressText;
        public string ValueTypeName;
        public string ValueText;
        public bool IsValid;
        public string Diagnostic;
        public bool HasStoredRecord;
        public PEGAKnowledgeStatus Status;
        public bool HasValue;
        public bool HasObservationTime;
        public double ObservationTimeSeconds;
        public PEGAKnowledgeInvalidationCause InvalidationCauses;
        public readonly List<PEGAKnowledgeEvidenceOrigin> Origins =
            new List<PEGAKnowledgeEvidenceOrigin>();
        public readonly List<PEGAKnowledgeRuntimeInspectorEvidence> Evidence =
            new List<PEGAKnowledgeRuntimeInspectorEvidence>();
    }

    internal sealed class PEGAKnowledgeRuntimeInspectorEvidence
    {
        public PEGAKnowledgeEvidenceOrigin Origin;
        public double ObservationTimeSeconds;
        public string Source;
        public string Payload;
    }

    /// <summary>Editor-only read-only projection of the authorities readable by the selected GOAP consumer.</summary>
    internal static class PEGAKnowledgeRuntimeInspectorSnapshotReader
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly FieldInfo IndividualStoreField =
            typeof(PEGANpcKnowledgeState).GetField("_factStore", InstancePrivate);
        private static readonly FieldInfo GlobalStoreField =
            typeof(PEGAGlobalKnowledgeState).GetField("_factStore", InstancePrivate);
        private static readonly FieldInfo GroupStoresField =
            typeof(PEGAActorGroupKnowledgeState).GetField("_factStores", InstancePrivate);
        private static readonly FieldInfo GroupAccessField =
            typeof(PEGAActorGroupKnowledgeState).GetField("_groupAccess", InstancePrivate);
        private static readonly FieldInfo GroupAuthoritiesField =
            typeof(PEGAActorGroupKnowledgeReader).GetField("authorityStates", InstancePrivate);
        private static readonly MethodInfo ReadTypedFactMethod =
            typeof(PEGAKnowledgeRuntimeInspectorSnapshotReader).GetMethod(
                nameof(ReadTypedFact), StaticPrivate);
        private static readonly MethodInfo ReadTypedGroupFactMethod =
            typeof(PEGAKnowledgeRuntimeInspectorSnapshotReader).GetMethod(
                nameof(ReadTypedGroupFact), StaticPrivate);

        public static PEGAKnowledgeRuntimeInspectorSnapshot Read(
            PEGANpcAutonomy autonomy,
            PEGANpcKnowledgeState individualState)
        {
            var snapshot = new PEGAKnowledgeRuntimeInspectorSnapshot
            {
                QueryTimeSeconds = Time.timeAsDouble,
                IndividualAuthorityStatus = "Authority Individual indisponível.",
                GlobalAuthorityStatus = "Authority Global não está composta no GOAP deste NPC.",
                GroupReaderStatus = "Leitor Group indisponível."
            };

            var keys = new HashSet<IPEGAKnowledgeFactKey>();
            var individualKeys = new HashSet<IPEGAKnowledgeFactKey>();
            IPEGAKnowledgeFactReader individualReader = individualState;
            PEGAKnowledgeFactStore individualStore = null;
            if (TryGetFactStore(
                    individualReader,
                    IndividualStoreField,
                    PEGAKnowledgeScope.Individual,
                    out individualStore,
                    out string individualIssue))
            {
                snapshot.IndividualAvailable = true;
                snapshot.IndividualAuthorityStatus = "Leitura disponível pela authority Individual do NPC.";
                if (!AddKeys(individualStore, individualKeys, keys, out string individualEnumerationIssue))
                {
                    snapshot.HasEnumerationIssues = true;
                    snapshot.IndividualAuthorityStatus += " " + individualEnumerationIssue;
                }
            }
            else
            {
                snapshot.IndividualAuthorityStatus = individualIssue;
            }

            IPEGAKnowledgeFactReader globalReader = FindGlobalReader(autonomy, out string globalIssue);
            PEGAKnowledgeFactStore globalStore = null;
            var globalKeys = new HashSet<IPEGAKnowledgeFactKey>();
            if (globalReader != null && TryGetFactStore(
                    globalReader,
                    GlobalStoreField,
                    PEGAKnowledgeScope.Global,
                    out globalStore,
                    out globalIssue))
            {
                snapshot.GlobalAvailable = true;
                snapshot.GlobalAuthorityStatus = "Leitura disponível pela capability Global composta no GOAP deste NPC.";
                if (!AddKeys(globalStore, globalKeys, keys, out string globalEnumerationIssue))
                {
                    snapshot.HasEnumerationIssues = true;
                    snapshot.GlobalAuthorityStatus += " " + globalEnumerationIssue;
                }
            }
            else
            {
                snapshot.GlobalAuthorityStatus = globalIssue;
            }

            PEGAActorGroupKnowledgeReader groupReader = autonomy != null
                ? autonomy.GetComponent<PEGAActorGroupKnowledgeReader>()
                : null;
            ActorId actorId = default;
            bool hasActorId = TryGetActorId(autonomy, out actorId, out string actorIssue);
            List<GroupContext> groups = ReadGroups(
                groupReader,
                actorId,
                hasActorId,
                actorIssue,
                keys,
                snapshot);

            snapshot.HasRegisteredFacts = keys.Count > 0;
            var orderedKeys = new List<IPEGAKnowledgeFactKey>(keys);
            orderedKeys.Sort(CompareKeys);
            if (snapshot.IndividualAvailable && individualReader != null)
            {
                for (int index = 0; index < orderedKeys.Count; index++)
                {
                    snapshot.IndividualFacts.Add(ReadFact(
                        individualReader,
                        orderedKeys[index],
                        snapshot.QueryTimeSeconds,
                        individualKeys.Contains(orderedKeys[index])));
                }
            }

            if (snapshot.GlobalAvailable && globalReader != null)
            {
                for (int index = 0; index < orderedKeys.Count; index++)
                {
                    snapshot.GlobalFacts.Add(ReadFact(
                        globalReader,
                        orderedKeys[index],
                        snapshot.QueryTimeSeconds,
                        globalKeys.Contains(orderedKeys[index])));
                }
            }

            for (int index = 0; index < groups.Count; index++)
            {
                PopulateGroupFacts(groups[index], groupReader, orderedKeys, snapshot.QueryTimeSeconds);
                snapshot.Groups.Add(groups[index].Snapshot);
            }

            snapshot.HasReadableAuthority = snapshot.IndividualAvailable || snapshot.GlobalAvailable;
            for (int index = 0; index < snapshot.Groups.Count; index++)
            {
                snapshot.HasReadableAuthority |= snapshot.Groups[index].CanRead;
            }

            return snapshot;
        }

        private static IPEGAKnowledgeFactReader FindGlobalReader(
            PEGANpcAutonomy autonomy,
            out string diagnostic)
        {
            diagnostic = "Authority Global não está composta no GOAP deste NPC.";
            if (autonomy == null || autonomy.GoapKnowledgeState == null ||
                autonomy.GoapKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                return null;
            }

            var readers = new List<IPEGAKnowledgeFactReader>();
            IReadOnlyList<IGoapKnowledgeCapability> capabilities = autonomy.GoapKnowledgeState.Capabilities;
            for (int index = 0; capabilities != null && index < capabilities.Count; index++)
            {
                if (capabilities[index] is IPEGAKnowledgeFactReader reader &&
                    reader.Scope == PEGAKnowledgeScope.Global)
                {
                    bool alreadyAdded = false;
                    for (int readerIndex = 0; readerIndex < readers.Count; readerIndex++)
                    {
                        if (ReferenceEquals(readers[readerIndex], reader))
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }

                    if (!alreadyAdded)
                    {
                        readers.Add(reader);
                    }
                }
            }

            if (readers.Count == 0)
            {
                return null;
            }

            if (readers.Count > 1)
            {
                diagnostic = "Mais de uma capability Global foi composta no GOAP deste NPC; leitura omitida por ambiguidade.";
                return null;
            }

            IPEGAKnowledgeFactReader selected = readers[0];
            if (!selected.IsAvailable)
            {
                diagnostic = "A capability Global está composta, mas sua authority da Activity não está disponível.";
                return null;
            }

            if (!(selected is PEGAGlobalKnowledgeState))
            {
                diagnostic = $"A capability Global '{selected.GetType().Name}' não expõe enumeração de chaves para este Inspector.";
                return null;
            }

            diagnostic = string.Empty;
            return selected;
        }

        private static List<GroupContext> ReadGroups(
            PEGAActorGroupKnowledgeReader reader,
            ActorId actorId,
            bool hasActorId,
            string actorIssue,
            HashSet<IPEGAKnowledgeFactKey> allKeys,
            PEGAKnowledgeRuntimeInspectorSnapshot snapshot)
        {
            var result = new List<GroupContext>();
            if (reader == null || !reader.isActiveAndEnabled)
            {
                snapshot.GroupReaderStatus = reader == null
                    ? "O NPC selecionado não possui PEGAActorGroupKnowledgeReader."
                    : "PEGAActorGroupKnowledgeReader está desativado.";
                return result;
            }

            if (GroupAuthoritiesField == null)
            {
                snapshot.GroupReaderStatus = "O campo de authorities do leitor Group não foi localizado; atualize o Inspector para esta versão do PEGA.";
                return result;
            }

            object rawAuthorities = ReadPrivateField(GroupAuthoritiesField, reader);
            if (rawAuthorities == null)
            {
                snapshot.GroupReaderStatus = "A lista de authorities Group está nula ou não pôde ser lida.";
                return result;
            }

            if (!(rawAuthorities is PEGAActorGroupKnowledgeState[] authorities) || authorities.Length == 0)
            {
                snapshot.GroupReaderStatus = "O leitor não tem authorities Group configuradas.";
                return result;
            }

            if (!hasActorId)
            {
                snapshot.GroupReaderStatus = $"Membership não pode ser consultada: {actorIssue}";
            }
            else
            {
                snapshot.GroupReaderStatus = "Membership consultada pelo Framework para cada Group vivo.";
            }

            var visitedAuthorities = new HashSet<PEGAActorGroupKnowledgeState>();
            var groupsById = new Dictionary<GroupId, GroupContext>();
            for (int index = 0; index < authorities.Length; index++)
            {
                PEGAActorGroupKnowledgeState authority = authorities[index];
                if (authority == null || !visitedAuthorities.Add(authority))
                {
                    continue;
                }

                object rawAccess = ReadPrivateField(GroupAccessField, authority);
                ActorGroupAccessBinding access = rawAccess as ActorGroupAccessBinding;
                var context = new GroupAuthorityContext(authority, access);
                if (!TryReadGroupStores(authority, context.Stores, out string storesIssue))
                {
                    snapshot.HasEnumerationIssues = true;
                    context.Issue = storesIssue;
                    snapshot.GroupReaderStatus += " " + storesIssue;
                }

                foreach (KeyValuePair<GroupId, PEGAKnowledgeFactStore> entry in context.Stores)
                {
                    AddGroupContext(groupsById, entry.Key, context);
                }
            }

            foreach (KeyValuePair<GroupId, GroupContext> entry in groupsById)
            {
                GroupContext context = entry.Value;
                PopulateGroupStatus(context, actorId, hasActorId);
                if (context.Snapshot.CanRead && context.Store != null)
                {
                    ReadAuthorizedGroupKeys(context, reader, allKeys, snapshot.QueryTimeSeconds, snapshot);
                }

                result.Add(context);
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.Snapshot.GroupId.StableText,
                right.Snapshot.GroupId.StableText));
            return result;
        }

        private static void AddGroupContext(
            IDictionary<GroupId, GroupContext> groupsById,
            GroupId groupId,
            GroupAuthorityContext authority)
        {
            if (!groupsById.TryGetValue(groupId, out GroupContext group))
            {
                group = new GroupContext(groupId);
                groupsById.Add(groupId, group);
            }

            group.AddAuthority(authority);
        }

        private static void PopulateGroupStatus(
            GroupContext context,
            ActorId actorId,
            bool hasActorId)
        {
            PEGAKnowledgeRuntimeInspectorGroup snapshot = context.Snapshot;
            if (context.StoreOwners.Count == 0)
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.Diagnostic = "Nenhuma authority Group configurada possui um store para este Group.";
                return;
            }

            if (context.StoreOwners.Count > 1)
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AmbiguousAuthority;
                snapshot.Diagnostic = "Mais de uma authority Group declara este Group; fatos ocultos.";
                return;
            }

            GroupAuthorityContext owner = context.StoreOwners[0];
            context.Store = owner.Stores[context.GroupId];
            if (!owner.State.IsAvailable || owner.Access == null || !owner.Access.IsAccessAvailable)
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.Diagnostic = string.IsNullOrWhiteSpace(owner.Issue)
                    ? "A authority Group ou seu binding Framework não está disponível."
                    : owner.Issue;
                return;
            }

            if (!owner.Access.TryGetGroup(context.GroupId, out ActorGroupSnapshot liveGroup, out string groupIssue) ||
                !liveGroup.IsValid || liveGroup.GroupId != context.GroupId)
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable;
                snapshot.Diagnostic = string.IsNullOrWhiteSpace(groupIssue)
                    ? "O Framework não confirmou este Group como vivo."
                    : groupIssue;
                return;
            }

            snapshot.DisplayName = liveGroup.Profile != null &&
                                   !string.IsNullOrWhiteSpace(liveGroup.Profile.DisplayName)
                ? liveGroup.Profile.DisplayName
                : context.GroupId.StableText;

            if (!hasActorId)
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.InvalidActor;
                snapshot.Diagnostic = "ActorId do NPC selecionado não está disponível.";
                return;
            }

            if (!owner.Access.TryIsMember(context.GroupId, actorId, out bool isMember, out string membershipIssue))
            {
                snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.Diagnostic = membershipIssue;
                return;
            }

            snapshot.IsCurrentMember = isMember;
            snapshot.AccessStatus = isMember
                ? PEGAActorGroupKnowledgeAccessStatus.Authorized
                : PEGAActorGroupKnowledgeAccessStatus.NotMember;
            snapshot.Diagnostic = isMember
                ? "Membership vigente confirmada pelo Framework."
                : "Membership vigente não autoriza a leitura; fatos ocultos.";
            snapshot.CanRead = isMember;
        }

        private static void ReadAuthorizedGroupKeys(
            GroupContext context,
            PEGAActorGroupKnowledgeReader reader,
            HashSet<IPEGAKnowledgeFactKey> allKeys,
            double queryTimeSeconds,
            PEGAKnowledgeRuntimeInspectorSnapshot snapshot)
        {
            // O chamador confirmou o Group vivo e a membership atual; aqui enumeramos apenas as chaves.
            // Cada valor é lido pelo leitor público Group do NPC selecionado.
            if (!TryGetKeys(context.Store, out List<IPEGAKnowledgeFactKey> keys, out string issue))
            {
                snapshot.HasEnumerationIssues = true;
                context.Snapshot.CanRead = false;
                context.Snapshot.AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                context.Snapshot.Diagnostic = issue;
                return;
            }

            var rows = new Dictionary<IPEGAKnowledgeFactKey, PEGAKnowledgeRuntimeInspectorFact>();
            var acceptedKeys = new List<IPEGAKnowledgeFactKey>();
            for (int index = 0; index < keys.Count; index++)
            {
                IPEGAKnowledgeFactKey key = keys[index];
                if (!TryDescribeKey(key, out _, out string keyIssue))
                {
                    rows[key] = InvalidFact(key, PEGAKnowledgeScope.Group, true, keyIssue);
                    acceptedKeys.Add(key);
                    continue;
                }

                GroupFactReadResult read = ReadGroupFact(reader, context.GroupId, key, queryTimeSeconds, true);
                if (read.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized)
                {
                    context.Snapshot.CanRead = false;
                    context.Snapshot.AccessStatus = read.AccessStatus;
                    if (read.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.NotMember)
                    {
                        context.Snapshot.IsCurrentMember = false;
                    }

                    context.Snapshot.Diagnostic = read.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.NotMember
                        ? "O leitor do NPC confirmou que a membership vigente não autoriza a leitura; fatos ocultos."
                        : "A leitura Group não foi autorizada pelo leitor do NPC; fatos ocultos.";
                    return;
                }

                rows[key] = read.Fact;
                acceptedKeys.Add(key);
            }

            context.FactsByKey = rows;
            context.StoredKeys = new HashSet<IPEGAKnowledgeFactKey>(acceptedKeys);
            for (int index = 0; index < acceptedKeys.Count; index++)
            {
                allKeys.Add(acceptedKeys[index]);
            }
        }

        private static void PopulateGroupFacts(
            GroupContext context,
            PEGAActorGroupKnowledgeReader reader,
            IReadOnlyList<IPEGAKnowledgeFactKey> allKeys,
            double queryTimeSeconds)
        {
            PEGAKnowledgeRuntimeInspectorGroup snapshot = context.Snapshot;
            if (!snapshot.CanRead || reader == null)
            {
                return;
            }

            for (int index = 0; index < allKeys.Count; index++)
            {
                IPEGAKnowledgeFactKey key = allKeys[index];
                if (!TryDescribeKey(key, out _, out string keyIssue))
                {
                    snapshot.Facts.Add(InvalidFact(
                        key,
                        PEGAKnowledgeScope.Group,
                        context.StoredKeys != null && context.StoredKeys.Contains(key),
                        keyIssue));
                    continue;
                }

                if (context.FactsByKey != null && context.FactsByKey.TryGetValue(key, out PEGAKnowledgeRuntimeInspectorFact storedFact))
                {
                    snapshot.Facts.Add(storedFact);
                    continue;
                }

                bool hasRecord = context.StoredKeys != null && context.StoredKeys.Contains(key);
                GroupFactReadResult read = ReadGroupFact(reader, context.GroupId, key, queryTimeSeconds, hasRecord);
                if (read.AccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized)
                {
                    snapshot.AccessStatus = read.AccessStatus;
                    snapshot.CanRead = false;
                    if (read.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.NotMember)
                    {
                        snapshot.IsCurrentMember = false;
                    }

                    snapshot.Diagnostic = read.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.NotMember
                        ? "O leitor do NPC confirmou que a membership vigente não autoriza a leitura; fatos ocultos."
                        : "Membership ou authority mudou durante a consulta; fatos ocultos.";
                    snapshot.Facts.Clear();
                    return;
                }

                snapshot.Facts.Add(read.Fact);
            }
        }

        private static GroupFactReadResult ReadGroupFact(
            PEGAActorGroupKnowledgeReader reader,
            GroupId groupId,
            IPEGAKnowledgeFactKey key,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            try
            {
                MethodInfo method = ReadTypedGroupFactMethod.MakeGenericMethod(key.AddressType, key.ValueType);
                return (GroupFactReadResult)method.Invoke(
                    null,
                    new object[] { reader, groupId, key, queryTimeSeconds, hasStoredRecord });
            }
            catch (Exception exception)
            {
                return new GroupFactReadResult(
                    PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable,
                    ReadFailure(key, PEGAKnowledgeScope.Group, hasStoredRecord, DescribeException(exception)));
            }
        }

        private static GroupFactReadResult ReadTypedGroupFact<TAddress, TValue>(
            PEGAActorGroupKnowledgeReader reader,
            GroupId groupId,
            object boxedKey,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            var key = (PEGAKnowledgeFactKey<TAddress, TValue>)boxedKey;
            PEGAActorGroupKnowledgeReadResult<TValue> result =
                reader.ReadGroupFact(groupId, key, queryTimeSeconds);
            return new GroupFactReadResult(
                result.AccessStatus,
                CreateFact(key, result.Fact, hasStoredRecord));
        }

        private static PEGAKnowledgeRuntimeInspectorFact ReadFact(
            IPEGAKnowledgeFactReader reader,
            IPEGAKnowledgeFactKey key,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            if (!TryDescribeKey(key, out _, out string issue))
            {
                return InvalidFact(key, reader.Scope, hasStoredRecord, issue);
            }

            try
            {
                MethodInfo method = ReadTypedFactMethod.MakeGenericMethod(key.AddressType, key.ValueType);
                return (PEGAKnowledgeRuntimeInspectorFact)method.Invoke(
                    null,
                    new object[] { reader, key, queryTimeSeconds, hasStoredRecord });
            }
            catch (Exception exception)
            {
                return ReadFailure(key, reader.Scope, hasStoredRecord, DescribeException(exception));
            }
        }

        private static PEGAKnowledgeRuntimeInspectorFact ReadTypedFact<TAddress, TValue>(
            IPEGAKnowledgeFactReader reader,
            object boxedKey,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            var key = (PEGAKnowledgeFactKey<TAddress, TValue>)boxedKey;
            PEGAKnowledgeFact<TValue> fact = reader.ReadFact(key, queryTimeSeconds);
            return CreateFact(key, fact, hasStoredRecord);
        }

        private static PEGAKnowledgeRuntimeInspectorFact CreateFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeFact<TValue> fact,
            bool hasStoredRecord)
        {
            if (!TryDescribeKey(key, out IPEGAKnowledgeFactDefinition definition, out string issue))
            {
                return InvalidFact(key, fact.Scope, hasStoredRecord, issue);
            }

            bool hasValue = fact.TryGetValue(out TValue value);
            var snapshot = new PEGAKnowledgeRuntimeInspectorFact
            {
                Scope = fact.Scope,
                Key = key,
                DefinitionId = definition.Id,
                DefinitionName = definition.Name,
                AddressTypeName = key.AddressType.Name,
                AddressText = FormatValue(key.AddressValue),
                ValueTypeName = key.ValueType.Name,
                ValueText = hasValue ? FormatValue(value) : "<sem valor conhecido>",
                IsValid = true,
                HasStoredRecord = hasStoredRecord,
                Status = fact.Status,
                HasValue = hasValue,
                HasObservationTime = fact.HasObservationTime,
                ObservationTimeSeconds = fact.ObservationTimeSeconds,
                InvalidationCauses = fact.InvalidationCauses
            };

            for (int index = 0; index < fact.Origins.Count; index++)
            {
                snapshot.Origins.Add(fact.Origins[index]);
            }

            for (int index = 0; index < fact.Evidence.Count; index++)
            {
                PEGAKnowledgeEvidence evidence = fact.Evidence[index];
                snapshot.Evidence.Add(new PEGAKnowledgeRuntimeInspectorEvidence
                {
                    Origin = evidence.Origin,
                    ObservationTimeSeconds = evidence.ObservationTimeSeconds,
                    Source = FormatEvidenceObject(evidence.Source),
                    Payload = FormatEvidenceObject(evidence.Payload)
                });
            }

            return snapshot;
        }

        private static bool TryDescribeKey(
            IPEGAKnowledgeFactKey key,
            out IPEGAKnowledgeFactDefinition definition,
            out string diagnostic)
        {
            definition = key?.DefinitionIdentity as IPEGAKnowledgeFactDefinition;
            diagnostic = string.Empty;
            if (key == null || !key.IsValid || definition == null)
            {
                diagnostic = "A chave ou seu endereço tipado é inválido.";
                return false;
            }

            try
            {
                if (!PEGAKnowledgeFactDefinitionCatalog.TryGet(definition.Id, out IPEGAKnowledgeFactDefinition canonical) ||
                    !ReferenceEquals(canonical, definition))
                {
                    diagnostic = $"A definição '{definition.Id}' não corresponde à instância canônica do catálogo.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                diagnostic = $"O catálogo não pôde validar a definição: {DescribeException(exception)}";
                return false;
            }

            if (definition.AddressType != key.AddressType || definition.ValueType != key.ValueType)
            {
                diagnostic = "Os tipos do endereço ou valor não correspondem à definição canônica.";
                return false;
            }

            return true;
        }

        private static PEGAKnowledgeRuntimeInspectorFact InvalidFact(
            IPEGAKnowledgeFactKey key,
            PEGAKnowledgeScope scope,
            bool hasStoredRecord,
            string diagnostic)
        {
            IPEGAKnowledgeFactDefinition definition = key?.DefinitionIdentity as IPEGAKnowledgeFactDefinition;
            object address = null;
            try
            {
                address = key?.Address;
            }
            catch
            {
                // Preserve the invalid row even when the typed address cannot be read.
            }

            return new PEGAKnowledgeRuntimeInspectorFact
            {
                Scope = scope,
                Key = key,
                DefinitionId = definition != null ? definition.Id : "<indisponível>",
                DefinitionName = definition != null ? definition.Name : "Definição inválida",
                AddressTypeName = key?.AddressType != null ? key.AddressType.Name : "<indisponível>",
                AddressText = FormatValue(address),
                ValueTypeName = key?.ValueType != null ? key.ValueType.Name : "<indisponível>",
                ValueText = "<não lido>",
                IsValid = false,
                Diagnostic = diagnostic,
                HasStoredRecord = hasStoredRecord,
                Status = PEGAKnowledgeStatus.Unknown
            };
        }

        private static PEGAKnowledgeRuntimeInspectorFact ReadFailure(
            IPEGAKnowledgeFactKey key,
            PEGAKnowledgeScope scope,
            bool hasStoredRecord,
            string diagnostic)
        {
            PEGAKnowledgeRuntimeInspectorFact snapshot = InvalidFact(
                key,
                scope,
                hasStoredRecord,
                diagnostic);
            snapshot.IsValid = true;
            return snapshot;
        }

        private static bool TryGetFactStore(
            object authority,
            FieldInfo storeField,
            PEGAKnowledgeScope expectedScope,
            out PEGAKnowledgeFactStore store,
            out string diagnostic)
        {
            store = null;
            if (!(authority is IPEGAKnowledgeFactReader reader))
            {
                diagnostic = "A authority não implementa a leitura tipada de Knowledge.";
                return false;
            }

            if (!reader.IsAvailable)
            {
                diagnostic = $"A authority {expectedScope} não está disponível no runtime atual.";
                return false;
            }

            if (storeField == null)
            {
                diagnostic = $"O campo privado do store {expectedScope} não foi localizado; o Inspector precisa ser atualizado para esta versão do PEGA.";
                return false;
            }

            try
            {
                store = storeField.GetValue(authority) as PEGAKnowledgeFactStore;
            }
            catch (Exception exception)
            {
                diagnostic = $"Não foi possível enumerar chaves da authority {expectedScope}: {DescribeException(exception)}";
                return false;
            }

            if (store == null)
            {
                diagnostic = $"A authority {expectedScope} está disponível, mas não expõe um store ativo.";
                return false;
            }

            if (store.Scope != expectedScope)
            {
                diagnostic = $"O store declarou escopo {store.Scope}, esperado {expectedScope}.";
                store = null;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool AddKeys(
            PEGAKnowledgeFactStore store,
            HashSet<IPEGAKnowledgeFactKey> destination,
            HashSet<IPEGAKnowledgeFactKey> union,
            out string diagnostic)
        {
            if (!TryGetKeys(store, out List<IPEGAKnowledgeFactKey> keys, out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < keys.Count; index++)
            {
                destination.Add(keys[index]);
                union.Add(keys[index]);
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool TryGetKeys(
            PEGAKnowledgeFactStore store,
            out List<IPEGAKnowledgeFactKey> keys,
            out string diagnostic)
        {
            keys = new List<IPEGAKnowledgeFactKey>();
            if (store == null)
            {
                diagnostic = "Store de fatos indisponível.";
                return false;
            }

            try
            {
                IReadOnlyList<IPEGAKnowledgeFactKey> stored = store.GetFactKeys();
                for (int index = 0; stored != null && index < stored.Count; index++)
                {
                    if (stored[index] != null)
                    {
                        keys.Add(stored[index]);
                    }
                }

                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = $"Não foi possível enumerar as chaves do store: {DescribeException(exception)}";
                return false;
            }
        }

        private static bool TryReadGroupStores(
            PEGAActorGroupKnowledgeState authority,
            Dictionary<GroupId, PEGAKnowledgeFactStore> stores,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (GroupStoresField == null)
            {
                diagnostic = "O mapa interno dos stores Group não foi localizado.";
                return false;
            }

            try
            {
                if (!(GroupStoresField.GetValue(authority) is IDictionary rawStores))
                {
                    diagnostic = "A authority Group não expõe seu mapa de stores.";
                    return false;
                }

                foreach (DictionaryEntry entry in rawStores)
                {
                    if (entry.Key is GroupId groupId && entry.Value is PEGAKnowledgeFactStore store)
                    {
                        stores[groupId] = store;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                diagnostic = $"Não foi possível enumerar os stores Group: {DescribeException(exception)}";
                return false;
            }
        }

        private static object ReadPrivateField(FieldInfo field, object target)
        {
            if (field == null || target == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetActorId(PEGANpcAutonomy autonomy, out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            ActorDeclaration declaration = autonomy != null
                ? autonomy.GetComponent<ActorDeclaration>()
                : null;
            if (declaration == null)
            {
                diagnostic = "ActorDeclaration não está composto no NPC selecionado.";
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
                if (actorId.IsValid)
                {
                    diagnostic = string.Empty;
                    return true;
                }

                diagnostic = "ActorId declarado é inválido.";
                return false;
            }
            catch (Exception exception)
            {
                diagnostic = $"ActorId não pôde ser resolvido: {DescribeException(exception)}";
                return false;
            }
        }

        private static int CompareKeys(IPEGAKnowledgeFactKey left, IPEGAKnowledgeFactKey right)
        {
            string leftDefinition = (left?.DefinitionIdentity as IPEGAKnowledgeFactDefinition)?.Id ?? string.Empty;
            string rightDefinition = (right?.DefinitionIdentity as IPEGAKnowledgeFactDefinition)?.Id ?? string.Empty;
            int definitionOrder = string.CompareOrdinal(leftDefinition, rightDefinition);
            if (definitionOrder != 0)
            {
                return definitionOrder;
            }

            int typeOrder = string.CompareOrdinal(left?.AddressType?.FullName, right?.AddressType?.FullName);
            return typeOrder != 0
                ? typeOrder
                : string.CompareOrdinal(FormatValue(left?.Address), FormatValue(right?.Address));
        }

        internal static string FormatValue(object value)
        {
            if (value == null)
            {
                return "<nulo>";
            }

            if (value is UnityEngine.Object unityObject)
            {
                return unityObject == null
                    ? "<objeto Unity destruído>"
                    : $"{unityObject.name} ({unityObject.GetType().Name})";
            }

            if (value is Vector3 vector3)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:R}, {1:R}, {2:R})",
                    vector3.x,
                    vector3.y,
                    vector3.z);
            }

            if (value is Vector2 vector2)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:R}, {1:R})",
                    vector2.x,
                    vector2.y);
            }

            PropertyInfo stableText = value.GetType().GetProperty(
                "StableText",
                BindingFlags.Instance | BindingFlags.Public);
            if (stableText != null && stableText.PropertyType == typeof(string))
            {
                try
                {
                    return stableText.GetValue(value) as string ?? value.ToString();
                }
                catch
                {
                    // Fall through to the value's own representation.
                }
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            try
            {
                string json = JsonUtility.ToJson(value);
                if (!string.IsNullOrWhiteSpace(json) && !string.Equals(json, "{}", StringComparison.Ordinal))
                {
                    return json;
                }
            }
            catch
            {
                // Unsupported values retain their own ToString representation.
            }

            return value.ToString();
        }

        private static string FormatEvidenceObject(object value)
        {
            if (value == null)
            {
                return "<nenhum>";
            }

            return $"{value.GetType().Name}: {FormatValue(value)}";
        }

        private static string DescribeException(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
            {
                exception = invocation.InnerException;
            }

            return $"{exception.GetType().Name}: {exception.Message}";
        }

        private sealed class GroupAuthorityContext
        {
            public readonly PEGAActorGroupKnowledgeState State;
            public readonly ActorGroupAccessBinding Access;
            public readonly Dictionary<GroupId, PEGAKnowledgeFactStore> Stores =
                new Dictionary<GroupId, PEGAKnowledgeFactStore>();
            public string Issue;

            public GroupAuthorityContext(PEGAActorGroupKnowledgeState state, ActorGroupAccessBinding access)
            {
                State = state;
                Access = access;
            }
        }

        private sealed class GroupContext
        {
            public readonly GroupId GroupId;
            public readonly PEGAKnowledgeRuntimeInspectorGroup Snapshot;
            public readonly List<GroupAuthorityContext> StoreOwners = new List<GroupAuthorityContext>();
            public PEGAKnowledgeFactStore Store;
            public HashSet<IPEGAKnowledgeFactKey> StoredKeys;
            public Dictionary<IPEGAKnowledgeFactKey, PEGAKnowledgeRuntimeInspectorFact> FactsByKey;

            public GroupContext(GroupId groupId)
            {
                GroupId = groupId;
                Snapshot = new PEGAKnowledgeRuntimeInspectorGroup
                {
                    GroupId = groupId,
                    DisplayName = groupId.StableText,
                    AccessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                };
            }

            public void AddAuthority(GroupAuthorityContext authority)
            {
                if (authority.Stores.TryGetValue(GroupId, out _))
                {
                    StoreOwners.Add(authority);
                }
            }
        }

        private readonly struct GroupFactReadResult
        {
            public GroupFactReadResult(
                PEGAActorGroupKnowledgeAccessStatus accessStatus,
                PEGAKnowledgeRuntimeInspectorFact fact)
            {
                AccessStatus = accessStatus;
                Fact = fact;
            }

            public PEGAActorGroupKnowledgeAccessStatus AccessStatus { get; }

            public PEGAKnowledgeRuntimeInspectorFact Fact { get; }
        }
    }
}
