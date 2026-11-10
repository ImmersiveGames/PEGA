using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PEGA.NPC.Knowledge.Authoring;
using PEGA.NPC.Knowledge.Core;
namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Metadados sem apagamento de tipo para uma definição tipada canônica de fato.</summary>
    public interface IPegaKnowledgeFactDefinition
    {
        string Id { get; }

        string Name { get; }

        Type AddressType { get; }

        Type ValueType { get; }

        bool AllowsDesignerAuthoredInitialFact { get; }
    }

    /// <summary>
    /// Definição estável de um fato e sua política. Reutilize a mesma instância em todos os produtores,
    /// leitores e escritores desse fato; o owner do domínio fornece seu endereço tipado.
    /// </summary>
    public sealed class PegaKnowledgeFactDefinition<TAddress, TValue> :
        IPegaKnowledgeFactDefinition,
        IPegaKnowledgeFactInitialAuthoringDefinition
    {
        private readonly Func<TAddress, bool> _isValidAddress;

        public PegaKnowledgeFactDefinition(
            string id,
            string name,
            IPegaKnowledgeFactPolicy<TValue> policy,
            Func<TAddress, bool> isValidAddress,
            bool allowsDesignerAuthoredInitialFact = true)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A stable fact definition id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A fact definition name is required.", nameof(name));
            }

            Id = id.Trim();
            Name = name.Trim();
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _isValidAddress = isValidAddress ?? throw new ArgumentNullException(nameof(isValidAddress));
            AllowsDesignerAuthoredInitialFact = allowsDesignerAuthoredInitialFact;
        }

        public string Id { get; }

        public string Name { get; }

        public Type AddressType => typeof(TAddress);

        public Type ValueType => typeof(TValue);

        public bool AllowsDesignerAuthoredInitialFact { get; }

        public IPegaKnowledgeFactPolicy<TValue> Policy { get; }

        public PegaKnowledgeFactKey<TAddress, TValue> For(TAddress address) =>
            new PegaKnowledgeFactKey<TAddress, TValue>(this, address);

        internal bool IsValidAddress(TAddress address) => _isValidAddress(address);

        public bool TryWriteInitialFact(
            object address,
            object value,
            IPegaKnowledgeFactWriter writer,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PegaKnowledgeWriteResult.RejectedInvalid;
            if (!AllowsDesignerAuthoredInitialFact)
            {
                diagnostic = $"Fact definition '{Id}' does not allow designer-authored initial facts.";
                return false;
            }

            if (writer == null || !evidence.IsValid)
            {
                diagnostic = "A valid Knowledge writer and evidence are required.";
                return false;
            }

            if (!(address is TAddress typedAddress))
            {
                diagnostic = $"Address payload does not match '{typeof(TAddress).FullName}'.";
                return false;
            }

            if (!(value is TValue typedValue))
            {
                diagnostic = $"Value payload does not match '{typeof(TValue).FullName}'.";
                return false;
            }

            PegaKnowledgeFactKey<TAddress, TValue> key = For(typedAddress);
            if (!key.IsValid)
            {
                diagnostic = "The typed fact address is invalid.";
                return false;
            }

            result = writer.ObserveFact(key, typedValue, evidence);
            diagnostic = string.Empty;
            return true;
        }
    }

    public interface IPegaKnowledgeFactKey : IEquatable<IPegaKnowledgeFactKey>
    {
        object DefinitionIdentity { get; }

        object Address { get; }

        string Name { get; }

        Type AddressType { get; }

        Type ValueType { get; }

        bool IsValid { get; }
    }

    /// <summary>Endereço tipado de um valor; igualdade combina a instância da definição e a identidade do domínio.</summary>
    public readonly struct PegaKnowledgeFactKey<TAddress, TValue> :
        IPegaKnowledgeFactKey,
        IEquatable<PegaKnowledgeFactKey<TAddress, TValue>>
    {
        internal PegaKnowledgeFactKey(PegaKnowledgeFactDefinition<TAddress, TValue> definition, TAddress address)
        {
            Definition = definition;
            AddressValue = address;
        }

        public PegaKnowledgeFactDefinition<TAddress, TValue> Definition { get; }

        public TAddress AddressValue { get; }

        object IPegaKnowledgeFactKey.DefinitionIdentity => Definition;

        object IPegaKnowledgeFactKey.Address => AddressValue;

        public string Name => Definition != null ? Definition.Name : string.Empty;

        public Type AddressType => typeof(TAddress);

        public Type ValueType => typeof(TValue);

        public bool IsValid => Definition != null && Definition.IsValidAddress(AddressValue);

        public bool Equals(PegaKnowledgeFactKey<TAddress, TValue> other) =>
            ReferenceEquals(Definition, other.Definition) &&
            EqualityComparer<TAddress>.Default.Equals(AddressValue, other.AddressValue);

        public bool Equals(IPegaKnowledgeFactKey other) =>
            other is PegaKnowledgeFactKey<TAddress, TValue> typed && Equals(typed);

        public override bool Equals(object obj) =>
            obj is PegaKnowledgeFactKey<TAddress, TValue> typed && Equals(typed);

        public override int GetHashCode()
        {
            unchecked
            {
                int definitionHash = Definition != null
                    ? RuntimeHelpers.GetHashCode(Definition)
                    : 0;
                return (definitionHash * 397) ^
                    (AddressValue is null ? 0 : EqualityComparer<TAddress>.Default.GetHashCode(AddressValue));
            }
        }

        public override string ToString() => Definition == null
            ? "InvalidKnowledgeFactKey"
            : string.Concat(Definition.Name, "[", AddressValue, "]");
    }
}
