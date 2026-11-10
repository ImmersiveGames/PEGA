using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Immersive.Framework.Actors;
using PEGA.GOAP;
using PEGA.NPC.Autonomy;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Global;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;

namespace PEGA.Diagnostics.Editor
{
    internal sealed class PegaKnowledgeRuntimeInspectorSnapshot
    {
        public double queryTimeSeconds;
        public bool hasRegisteredFacts;
        public bool hasReadableAuthority;
        public bool hasEnumerationIssues;
        public bool individualAvailable;
        public string individualAuthorityStatus;
        public readonly List<PegaKnowledgeRuntimeInspectorFact> individualFacts =
            new List<PegaKnowledgeRuntimeInspectorFact>();
        public bool globalAvailable;
        public string globalAuthorityStatus;
        public readonly List<PegaKnowledgeRuntimeInspectorFact> globalFacts =
            new List<PegaKnowledgeRuntimeInspectorFact>();
        public string groupReaderStatus;
        public readonly List<PegaKnowledgeRuntimeInspectorGroup> groups =
            new List<PegaKnowledgeRuntimeInspectorGroup>();
    }

    internal sealed class PegaKnowledgeRuntimeInspectorGroup
    {
        public GroupId groupId;
        public string displayName;
        public bool? isCurrentMember;
        public bool canRead;
        public PegaActorGroupKnowledgeAccessStatus accessStatus;
        public string diagnostic;
        public readonly List<PegaKnowledgeRuntimeInspectorFact> facts =
            new List<PegaKnowledgeRuntimeInspectorFact>();
    }

    internal sealed class PegaKnowledgeRuntimeInspectorFact
    {
        public PegaKnowledgeScope scope;
        public IPegaKnowledgeFactKey key;
        public string definitionId;
        public string definitionName;
        public string addressTypeName;
        public string addressText;
        public string valueTypeName;
        public string valueText;
        public bool isValid;
        public string diagnostic;
        public bool hasStoredRecord;
        public PegaKnowledgeStatus status;
        public bool hasValue;
        public bool hasObservationTime;
        public double observationTimeSeconds;
        public PegaKnowledgeInvalidationCause invalidationCauses;
        public readonly List<PegaKnowledgeEvidenceOrigin> origins =
            new List<PegaKnowledgeEvidenceOrigin>();
        public readonly List<PegaKnowledgeRuntimeInspectorEvidence> evidence =
            new List<PegaKnowledgeRuntimeInspectorEvidence>();
    }

    internal sealed class PegaKnowledgeRuntimeInspectorEvidence
    {
        public PegaKnowledgeEvidenceOrigin origin;
        public double observationTimeSeconds;
        public string source;
        public string payload;
    }

    /// <summary>Editor-only read-only projection of the authorities readable by the selected GOAP consumer.</summary>
    internal static class PegaKnowledgeRuntimeInspectorSnapshotReader
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly FieldInfo IndividualStoreField =
            typeof(PegaNpcKnowledgeState).GetField("_factStore", InstancePrivate);
        private static readonly FieldInfo GlobalStoreField =
            typeof(PegaGlobalKnowledgeState).GetField("_factStore", InstancePrivate);
        private static readonly FieldInfo GroupStoresField =
            typeof(PegaActorGroupKnowledgeState).GetField("_factStores", InstancePrivate);
        private static readonly FieldInfo GroupAccessField =
            typeof(PegaActorGroupKnowledgeState).GetField("_groupAccess", InstancePrivate);
        private static readonly FieldInfo GroupAuthoritiesField =
            typeof(PegaActorGroupKnowledgeReader).GetField("authorityStates", InstancePrivate);
        private static readonly MethodInfo ReadTypedFactMethod =
            typeof(PegaKnowledgeRuntimeInspectorSnapshotReader).GetMethod(
                nameof(ReadTypedFact), StaticPrivate);
        private static readonly MethodInfo ReadTypedGroupFactMethod =
            typeof(PegaKnowledgeRuntimeInspectorSnapshotReader).GetMethod(
                nameof(ReadTypedGroupFact), StaticPrivate);

        public static PegaKnowledgeRuntimeInspectorSnapshot Read(
            PegaNpcAutonomy autonomy,
            PegaNpcKnowledgeState individualState)
        {
            var snapshot = new PegaKnowledgeRuntimeInspectorSnapshot
            {
                queryTimeSeconds = Time.timeAsDouble,
                individualAuthorityStatus = "Authority Individual indisponível.",
                globalAuthorityStatus = "Authority Global não está composta no GOAP deste NPC.",
                groupReaderStatus = "Leitor Group indisponível."
            };

            var keys = new HashSet<IPegaKnowledgeFactKey>();
            var individualKeys = new HashSet<IPegaKnowledgeFactKey>();
            IPegaKnowledgeFactReader individualReader = individualState;
            PegaKnowledgeFactStore individualStore = null;
            if (TryGetFactStore(
                    individualReader,
                    IndividualStoreField,
                    PegaKnowledgeScope.Individual,
                    out individualStore,
                    out string individualIssue))
            {
                snapshot.individualAvailable = true;
                snapshot.individualAuthorityStatus = "Leitura disponível pela authority Individual do NPC.";
                if (!AddKeys(individualStore, individualKeys, keys, out string individualEnumerationIssue))
                {
                    snapshot.hasEnumerationIssues = true;
                    snapshot.individualAuthorityStatus += " " + individualEnumerationIssue;
                }
            }
            else
            {
                snapshot.individualAuthorityStatus = individualIssue;
            }

            IPegaKnowledgeFactReader globalReader = FindGlobalReader(autonomy, out string globalIssue);
            PegaKnowledgeFactStore globalStore = null;
            var globalKeys = new HashSet<IPegaKnowledgeFactKey>();
            if (globalReader != null && TryGetFactStore(
                    globalReader,
                    GlobalStoreField,
                    PegaKnowledgeScope.Global,
                    out globalStore,
                    out globalIssue))
            {
                snapshot.globalAvailable = true;
                snapshot.globalAuthorityStatus = "Leitura disponível pela capability Global composta no GOAP deste NPC.";
                if (!AddKeys(globalStore, globalKeys, keys, out string globalEnumerationIssue))
                {
                    snapshot.hasEnumerationIssues = true;
                    snapshot.globalAuthorityStatus += " " + globalEnumerationIssue;
                }
            }
            else
            {
                snapshot.globalAuthorityStatus = globalIssue;
            }

            PegaActorGroupKnowledgeReader groupReader = autonomy != null
                ? autonomy.GetComponent<PegaActorGroupKnowledgeReader>()
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

            snapshot.hasRegisteredFacts = keys.Count > 0;
            var orderedKeys = new List<IPegaKnowledgeFactKey>(keys);
            orderedKeys.Sort(CompareKeys);
            if (snapshot.individualAvailable && individualReader != null)
            {
                for (int index = 0; index < orderedKeys.Count; index++)
                {
                    snapshot.individualFacts.Add(ReadFact(
                        individualReader,
                        orderedKeys[index],
                        snapshot.queryTimeSeconds,
                        individualKeys.Contains(orderedKeys[index])));
                }
            }

            if (snapshot.globalAvailable && globalReader != null)
            {
                for (int index = 0; index < orderedKeys.Count; index++)
                {
                    snapshot.globalFacts.Add(ReadFact(
                        globalReader,
                        orderedKeys[index],
                        snapshot.queryTimeSeconds,
                        globalKeys.Contains(orderedKeys[index])));
                }
            }

            for (int index = 0; index < groups.Count; index++)
            {
                PopulateGroupFacts(groups[index], groupReader, orderedKeys, snapshot.queryTimeSeconds);
                snapshot.groups.Add(groups[index].snapshot);
            }

            snapshot.hasReadableAuthority = snapshot.individualAvailable || snapshot.globalAvailable;
            for (int index = 0; index < snapshot.groups.Count; index++)
            {
                snapshot.hasReadableAuthority |= snapshot.groups[index].canRead;
            }

            return snapshot;
        }

        private static IPegaKnowledgeFactReader FindGlobalReader(
            PegaNpcAutonomy autonomy,
            out string diagnostic)
        {
            diagnostic = "Authority Global não está composta no GOAP deste NPC.";
            if (autonomy == null || autonomy.GoapKnowledgeState == null ||
                autonomy.GoapKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                return null;
            }

            var readers = new List<IPegaKnowledgeFactReader>();
            IReadOnlyList<IGoapKnowledgeCapability> capabilities = autonomy.GoapKnowledgeState.Capabilities;
            for (int index = 0; capabilities != null && index < capabilities.Count; index++)
            {
                if (capabilities[index] is IPegaKnowledgeFactReader reader &&
                    reader.Scope == PegaKnowledgeScope.Global)
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

            IPegaKnowledgeFactReader selected = readers[0];
            if (!selected.IsAvailable)
            {
                diagnostic = "A capability Global está composta, mas sua authority da Activity não está disponível.";
                return null;
            }

            if (!(selected is PegaGlobalKnowledgeState))
            {
                diagnostic = $"A capability Global '{selected.GetType().Name}' não expõe enumeração de chaves para este Inspector.";
                return null;
            }

            diagnostic = string.Empty;
            return selected;
        }

        private static List<GroupContext> ReadGroups(
            PegaActorGroupKnowledgeReader reader,
            ActorId actorId,
            bool hasActorId,
            string actorIssue,
            HashSet<IPegaKnowledgeFactKey> allKeys,
            PegaKnowledgeRuntimeInspectorSnapshot snapshot)
        {
            var result = new List<GroupContext>();
            if (reader == null || !reader.isActiveAndEnabled)
            {
                snapshot.groupReaderStatus = reader == null
                    ? "O NPC selecionado não possui PEGAActorGroupKnowledgeReader."
                    : "PEGAActorGroupKnowledgeReader está desativado.";
                return result;
            }

            if (GroupAuthoritiesField == null)
            {
                snapshot.groupReaderStatus = "O campo de authorities do leitor Group não foi localizado; atualize o Inspector para esta versão do PEGA.";
                return result;
            }

            object rawAuthorities = ReadPrivateField(GroupAuthoritiesField, reader);
            if (rawAuthorities == null)
            {
                snapshot.groupReaderStatus = "A lista de authorities Group está nula ou não pôde ser lida.";
                return result;
            }

            if (!(rawAuthorities is PegaActorGroupKnowledgeState[] authorities) || authorities.Length == 0)
            {
                snapshot.groupReaderStatus = "O leitor não tem authorities Group configuradas.";
                return result;
            }

            if (!hasActorId)
            {
                snapshot.groupReaderStatus = $"Membership não pode ser consultada: {actorIssue}";
            }
            else
            {
                snapshot.groupReaderStatus = "Membership consultada pelo Framework para cada Group vivo.";
            }

            var visitedAuthorities = new HashSet<PegaActorGroupKnowledgeState>();
            var groupsById = new Dictionary<GroupId, GroupContext>();
            for (int index = 0; index < authorities.Length; index++)
            {
                PegaActorGroupKnowledgeState authority = authorities[index];
                if (authority == null || !visitedAuthorities.Add(authority))
                {
                    continue;
                }

                object rawAccess = ReadPrivateField(GroupAccessField, authority);
                ActorGroupAccessBinding access = rawAccess as ActorGroupAccessBinding;
                var context = new GroupAuthorityContext(authority, access);
                if (!TryReadGroupStores(authority, context.stores, out string storesIssue))
                {
                    snapshot.hasEnumerationIssues = true;
                    context.issue = storesIssue;
                    snapshot.groupReaderStatus += " " + storesIssue;
                }

                foreach (KeyValuePair<GroupId, PegaKnowledgeFactStore> entry in context.stores)
                {
                    AddGroupContext(groupsById, entry.Key, context);
                }
            }

            foreach (KeyValuePair<GroupId, GroupContext> entry in groupsById)
            {
                GroupContext context = entry.Value;
                PopulateGroupStatus(context, actorId, hasActorId);
                if (context.snapshot.canRead && context.store != null)
                {
                    ReadAuthorizedGroupKeys(context, reader, allKeys, snapshot.queryTimeSeconds, snapshot);
                }

                result.Add(context);
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.snapshot.groupId.StableText,
                right.snapshot.groupId.StableText));
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
            PegaKnowledgeRuntimeInspectorGroup snapshot = context.snapshot;
            if (context.storeOwners.Count == 0)
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.diagnostic = "Nenhuma authority Group configurada possui um store para este Group.";
                return;
            }

            if (context.storeOwners.Count > 1)
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.AmbiguousAuthority;
                snapshot.diagnostic = "Mais de uma authority Group declara este Group; fatos ocultos.";
                return;
            }

            GroupAuthorityContext owner = context.storeOwners[0];
            context.store = owner.stores[context.groupId];
            if (!owner.state.IsAvailable || owner.access == null || !owner.access.IsAccessAvailable)
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.diagnostic = string.IsNullOrWhiteSpace(owner.issue)
                    ? "A authority Group ou seu binding Framework não está disponível."
                    : owner.issue;
                return;
            }

            if (!owner.access.TryGetGroup(context.groupId, out ActorGroupSnapshot liveGroup, out string groupIssue) ||
                !liveGroup.IsValid || liveGroup.GroupId != context.groupId)
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.GroupUnavailable;
                snapshot.diagnostic = string.IsNullOrWhiteSpace(groupIssue)
                    ? "O Framework não confirmou este Group como vivo."
                    : groupIssue;
                return;
            }

            snapshot.displayName = liveGroup.Profile != null &&
                                   !string.IsNullOrWhiteSpace(liveGroup.Profile.DisplayName)
                ? liveGroup.Profile.DisplayName
                : context.groupId.StableText;

            if (!hasActorId)
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.InvalidActor;
                snapshot.diagnostic = "ActorId do NPC selecionado não está disponível.";
                return;
            }

            if (!owner.access.TryIsMember(context.groupId, actorId, out bool isMember, out string membershipIssue))
            {
                snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                snapshot.diagnostic = membershipIssue;
                return;
            }

            snapshot.isCurrentMember = isMember;
            snapshot.accessStatus = isMember
                ? PegaActorGroupKnowledgeAccessStatus.Authorized
                : PegaActorGroupKnowledgeAccessStatus.NotMember;
            snapshot.diagnostic = isMember
                ? "Membership vigente confirmada pelo Framework."
                : "Membership vigente não autoriza a leitura; fatos ocultos.";
            snapshot.canRead = isMember;
        }

        private static void ReadAuthorizedGroupKeys(
            GroupContext context,
            PegaActorGroupKnowledgeReader reader,
            HashSet<IPegaKnowledgeFactKey> allKeys,
            double queryTimeSeconds,
            PegaKnowledgeRuntimeInspectorSnapshot snapshot)
        {
            // O chamador confirmou o Group vivo e a membership atual; aqui enumeramos apenas as chaves.
            // Cada valor é lido pelo leitor público Group do NPC selecionado.
            if (!TryGetKeys(context.store, out List<IPegaKnowledgeFactKey> keys, out string issue))
            {
                snapshot.hasEnumerationIssues = true;
                context.snapshot.canRead = false;
                context.snapshot.accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                context.snapshot.diagnostic = issue;
                return;
            }

            var rows = new Dictionary<IPegaKnowledgeFactKey, PegaKnowledgeRuntimeInspectorFact>();
            var acceptedKeys = new List<IPegaKnowledgeFactKey>();
            for (int index = 0; index < keys.Count; index++)
            {
                IPegaKnowledgeFactKey key = keys[index];
                if (!TryDescribeKey(key, out _, out string keyIssue))
                {
                    rows[key] = InvalidFact(key, PegaKnowledgeScope.Group, true, keyIssue);
                    acceptedKeys.Add(key);
                    continue;
                }

                GroupFactReadResult read = ReadGroupFact(reader, context.groupId, key, queryTimeSeconds, true);
                if (read.AccessStatus != PegaActorGroupKnowledgeAccessStatus.Authorized)
                {
                    context.snapshot.canRead = false;
                    context.snapshot.accessStatus = read.AccessStatus;
                    if (read.AccessStatus == PegaActorGroupKnowledgeAccessStatus.NotMember)
                    {
                        context.snapshot.isCurrentMember = false;
                    }

                    context.snapshot.diagnostic = read.AccessStatus == PegaActorGroupKnowledgeAccessStatus.NotMember
                        ? "O leitor do NPC confirmou que a membership vigente não autoriza a leitura; fatos ocultos."
                        : "A leitura Group não foi autorizada pelo leitor do NPC; fatos ocultos.";
                    return;
                }

                rows[key] = read.Fact;
                acceptedKeys.Add(key);
            }

            context.factsByKey = rows;
            context.storedKeys = new HashSet<IPegaKnowledgeFactKey>(acceptedKeys);
            for (int index = 0; index < acceptedKeys.Count; index++)
            {
                allKeys.Add(acceptedKeys[index]);
            }
        }

        private static void PopulateGroupFacts(
            GroupContext context,
            PegaActorGroupKnowledgeReader reader,
            IReadOnlyList<IPegaKnowledgeFactKey> allKeys,
            double queryTimeSeconds)
        {
            PegaKnowledgeRuntimeInspectorGroup snapshot = context.snapshot;
            if (!snapshot.canRead || reader == null)
            {
                return;
            }

            for (int index = 0; index < allKeys.Count; index++)
            {
                IPegaKnowledgeFactKey key = allKeys[index];
                if (!TryDescribeKey(key, out _, out string keyIssue))
                {
                    snapshot.facts.Add(InvalidFact(
                        key,
                        PegaKnowledgeScope.Group,
                        context.storedKeys != null && context.storedKeys.Contains(key),
                        keyIssue));
                    continue;
                }

                if (context.factsByKey != null && context.factsByKey.TryGetValue(key, out PegaKnowledgeRuntimeInspectorFact storedFact))
                {
                    snapshot.facts.Add(storedFact);
                    continue;
                }

                bool hasRecord = context.storedKeys != null && context.storedKeys.Contains(key);
                GroupFactReadResult read = ReadGroupFact(reader, context.groupId, key, queryTimeSeconds, hasRecord);
                if (read.AccessStatus != PegaActorGroupKnowledgeAccessStatus.Authorized)
                {
                    snapshot.accessStatus = read.AccessStatus;
                    snapshot.canRead = false;
                    if (read.AccessStatus == PegaActorGroupKnowledgeAccessStatus.NotMember)
                    {
                        snapshot.isCurrentMember = false;
                    }

                    snapshot.diagnostic = read.AccessStatus == PegaActorGroupKnowledgeAccessStatus.NotMember
                        ? "O leitor do NPC confirmou que a membership vigente não autoriza a leitura; fatos ocultos."
                        : "Membership ou authority mudou durante a consulta; fatos ocultos.";
                    snapshot.facts.Clear();
                    return;
                }

                snapshot.facts.Add(read.Fact);
            }
        }

        private static GroupFactReadResult ReadGroupFact(
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            IPegaKnowledgeFactKey key,
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
                    PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable,
                    ReadFailure(key, PegaKnowledgeScope.Group, hasStoredRecord, DescribeException(exception)));
            }
        }

        private static GroupFactReadResult ReadTypedGroupFact<TAddress, TValue>(
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            object boxedKey,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            var key = (PegaKnowledgeFactKey<TAddress, TValue>)boxedKey;
            PegaActorGroupKnowledgeReadResult<TValue> result =
                reader.ReadGroupFact(groupId, key, queryTimeSeconds);
            return new GroupFactReadResult(
                result.AccessStatus,
                CreateFact(key, result.Fact, hasStoredRecord));
        }

        private static PegaKnowledgeRuntimeInspectorFact ReadFact(
            IPegaKnowledgeFactReader reader,
            IPegaKnowledgeFactKey key,
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
                return (PegaKnowledgeRuntimeInspectorFact)method.Invoke(
                    null,
                    new object[] { reader, key, queryTimeSeconds, hasStoredRecord });
            }
            catch (Exception exception)
            {
                return ReadFailure(key, reader.Scope, hasStoredRecord, DescribeException(exception));
            }
        }

        private static PegaKnowledgeRuntimeInspectorFact ReadTypedFact<TAddress, TValue>(
            IPegaKnowledgeFactReader reader,
            object boxedKey,
            double queryTimeSeconds,
            bool hasStoredRecord)
        {
            var key = (PegaKnowledgeFactKey<TAddress, TValue>)boxedKey;
            PegaKnowledgeFact<TValue> fact = reader.ReadFact(key, queryTimeSeconds);
            return CreateFact(key, fact, hasStoredRecord);
        }

        private static PegaKnowledgeRuntimeInspectorFact CreateFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeFact<TValue> fact,
            bool hasStoredRecord)
        {
            if (!TryDescribeKey(key, out IPegaKnowledgeFactDefinition definition, out string issue))
            {
                return InvalidFact(key, fact.Scope, hasStoredRecord, issue);
            }

            bool hasValue = fact.TryGetValue(out TValue value);
            var snapshot = new PegaKnowledgeRuntimeInspectorFact
            {
                scope = fact.Scope,
                key = key,
                definitionId = definition.Id,
                definitionName = definition.Name,
                addressTypeName = key.AddressType.Name,
                addressText = FormatValue(key.AddressValue),
                valueTypeName = key.ValueType.Name,
                valueText = hasValue ? FormatValue(value) : "<sem valor conhecido>",
                isValid = true,
                hasStoredRecord = hasStoredRecord,
                status = fact.Status,
                hasValue = hasValue,
                hasObservationTime = fact.HasObservationTime,
                observationTimeSeconds = fact.ObservationTimeSeconds,
                invalidationCauses = fact.InvalidationCauses
            };

            for (int index = 0; index < fact.Origins.Count; index++)
            {
                snapshot.origins.Add(fact.Origins[index]);
            }

            for (int index = 0; index < fact.Evidence.Count; index++)
            {
                PegaKnowledgeEvidence evidence = fact.Evidence[index];
                snapshot.evidence.Add(new PegaKnowledgeRuntimeInspectorEvidence
                {
                    origin = evidence.Origin,
                    observationTimeSeconds = evidence.ObservationTimeSeconds,
                    source = FormatEvidenceObject(evidence.Source),
                    payload = FormatEvidenceObject(evidence.Payload)
                });
            }

            return snapshot;
        }

        private static bool TryDescribeKey(
            IPegaKnowledgeFactKey key,
            out IPegaKnowledgeFactDefinition definition,
            out string diagnostic)
        {
            definition = key?.DefinitionIdentity as IPegaKnowledgeFactDefinition;
            diagnostic = string.Empty;
            if (key == null || !key.IsValid || definition == null)
            {
                diagnostic = "A chave ou seu endereço tipado é inválido.";
                return false;
            }

            try
            {
                if (!PegaKnowledgeFactDefinitionCatalog.TryGet(definition.Id, out IPegaKnowledgeFactDefinition canonical) ||
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

        private static PegaKnowledgeRuntimeInspectorFact InvalidFact(
            IPegaKnowledgeFactKey key,
            PegaKnowledgeScope scope,
            bool hasStoredRecord,
            string diagnostic)
        {
            IPegaKnowledgeFactDefinition definition = key?.DefinitionIdentity as IPegaKnowledgeFactDefinition;
            object address = null;
            try
            {
                address = key?.Address;
            }
            catch
            {
                // Preserve the invalid row even when the typed address cannot be read.
            }

            return new PegaKnowledgeRuntimeInspectorFact
            {
                scope = scope,
                key = key,
                definitionId = definition != null ? definition.Id : "<indisponível>",
                definitionName = definition != null ? definition.Name : "Definição inválida",
                addressTypeName = key?.AddressType != null ? key.AddressType.Name : "<indisponível>",
                addressText = FormatValue(address),
                valueTypeName = key?.ValueType != null ? key.ValueType.Name : "<indisponível>",
                valueText = "<não lido>",
                isValid = false,
                diagnostic = diagnostic,
                hasStoredRecord = hasStoredRecord,
                status = PegaKnowledgeStatus.Unknown
            };
        }

        private static PegaKnowledgeRuntimeInspectorFact ReadFailure(
            IPegaKnowledgeFactKey key,
            PegaKnowledgeScope scope,
            bool hasStoredRecord,
            string diagnostic)
        {
            PegaKnowledgeRuntimeInspectorFact snapshot = InvalidFact(
                key,
                scope,
                hasStoredRecord,
                diagnostic);
            snapshot.isValid = true;
            return snapshot;
        }

        private static bool TryGetFactStore(
            object authority,
            FieldInfo storeField,
            PegaKnowledgeScope expectedScope,
            out PegaKnowledgeFactStore store,
            out string diagnostic)
        {
            store = null;
            if (!(authority is IPegaKnowledgeFactReader reader))
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
                store = storeField.GetValue(authority) as PegaKnowledgeFactStore;
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
            PegaKnowledgeFactStore store,
            HashSet<IPegaKnowledgeFactKey> destination,
            HashSet<IPegaKnowledgeFactKey> union,
            out string diagnostic)
        {
            if (!TryGetKeys(store, out List<IPegaKnowledgeFactKey> keys, out diagnostic))
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
            PegaKnowledgeFactStore store,
            out List<IPegaKnowledgeFactKey> keys,
            out string diagnostic)
        {
            keys = new List<IPegaKnowledgeFactKey>();
            if (store == null)
            {
                diagnostic = "Store de fatos indisponível.";
                return false;
            }

            try
            {
                IReadOnlyList<IPegaKnowledgeFactKey> stored = store.GetFactKeys();
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
            PegaActorGroupKnowledgeState authority,
            Dictionary<GroupId, PegaKnowledgeFactStore> stores,
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
                    if (entry.Key is GroupId groupId && entry.Value is PegaKnowledgeFactStore store)
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

        private static bool TryGetActorId(PegaNpcAutonomy autonomy, out ActorId actorId, out string diagnostic)
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

        private static int CompareKeys(IPegaKnowledgeFactKey left, IPegaKnowledgeFactKey right)
        {
            string leftDefinition = (left?.DefinitionIdentity as IPegaKnowledgeFactDefinition)?.Id ?? string.Empty;
            string rightDefinition = (right?.DefinitionIdentity as IPegaKnowledgeFactDefinition)?.Id ?? string.Empty;
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
            public readonly PegaActorGroupKnowledgeState state;
            public readonly ActorGroupAccessBinding access;
            public readonly Dictionary<GroupId, PegaKnowledgeFactStore> stores =
                new Dictionary<GroupId, PegaKnowledgeFactStore>();
            public string issue;

            public GroupAuthorityContext(PegaActorGroupKnowledgeState state, ActorGroupAccessBinding access)
            {
                this.state = state;
                this.access = access;
            }
        }

        private sealed class GroupContext
        {
            public readonly GroupId groupId;
            public readonly PegaKnowledgeRuntimeInspectorGroup snapshot;
            public readonly List<GroupAuthorityContext> storeOwners = new List<GroupAuthorityContext>();
            public PegaKnowledgeFactStore store;
            public HashSet<IPegaKnowledgeFactKey> storedKeys;
            public Dictionary<IPegaKnowledgeFactKey, PegaKnowledgeRuntimeInspectorFact> factsByKey;

            public GroupContext(GroupId groupId)
            {
                this.groupId = groupId;
                snapshot = new PegaKnowledgeRuntimeInspectorGroup
                {
                    groupId = groupId,
                    displayName = groupId.StableText,
                    accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                };
            }

            public void AddAuthority(GroupAuthorityContext authority)
            {
                if (authority.stores.TryGetValue(groupId, out _))
                {
                    storeOwners.Add(authority);
                }
            }
        }

        private readonly struct GroupFactReadResult
        {
            public GroupFactReadResult(
                PegaActorGroupKnowledgeAccessStatus accessStatus,
                PegaKnowledgeRuntimeInspectorFact fact)
            {
                AccessStatus = accessStatus;
                Fact = fact;
            }

            public PegaActorGroupKnowledgeAccessStatus AccessStatus { get; }

            public PegaKnowledgeRuntimeInspectorFact Fact { get; }
        }
    }
}
