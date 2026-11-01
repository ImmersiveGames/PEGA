using System;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PEGAActorGroupInitialKnowledgeFactMenuAttribute : Attribute
    {
        public PEGAActorGroupInitialKnowledgeFactMenuAttribute(string menuName)
        {
            MenuName = string.IsNullOrWhiteSpace(menuName)
                ? throw new ArgumentException("A menu name is required.", nameof(menuName))
                : menuName;
        }

        public string MenuName { get; }
    }

    /// <summary>Dados iniciais autorados no Unity e aplicados ao armazenamento genérico do Group proprietário.</summary>
    [Serializable]
    public abstract class PEGAActorGroupInitialKnowledgeFact
    {
        internal abstract string FactName { get; }

        internal abstract bool TryObserve(
            PEGAKnowledgeFactStore factStore,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic);

        protected static bool TryObserveDefinition<TAddress, TValue>(
            PEGAKnowledgeFactStore factStore,
            PEGAKnowledgeFactDefinition<TAddress, TValue> definition,
            TAddress address,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (factStore == null || definition == null)
            {
                diagnostic = "A fact store and an available fact definition are required.";
                return false;
            }

            PEGAKnowledgeFactKey<TAddress, TValue> key = definition.For(address);
            if (!key.IsValid)
            {
                diagnostic = $"Fact definition '{definition.Name}' rejected its authored address.";
                return false;
            }

            result = factStore.Observe(key, value, evidence);
            if (result == PEGAKnowledgeWriteResult.RejectedInvalid)
            {
                diagnostic = $"Fact definition '{definition.Name}' rejected its authored value or evidence.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }

    [Serializable]
    [PEGAActorGroupInitialKnowledgeFactMenu("Presença em área")]
    public sealed class PEGAActorGroupInitialAreaPresenceFact : PEGAActorGroupInitialKnowledgeFact
    {
        [SerializeField]
        [Tooltip("Indica se a informação inicial declara presença nesta área.")]
        private bool isPresent = true;

        [SerializeField]
        [Tooltip("Posição conhecida que ancora este fato de presença em área.")]
        private Vector3 observerPosition;

        [SerializeField, Min(0.01f)]
        [Tooltip("Raio da área em metros.")]
        private float perceivedAreaRadiusMeters = 5f;

        internal override string FactName => PEGANpcKnowledgeFacts.PerceivedAreaPresence.Name;

        internal override bool TryObserve(
            PEGAKnowledgeFactStore factStore,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            var value = new PEGANpcPerceivedAreaPresence(
                isPresent,
                observerPosition,
                perceivedAreaRadiusMeters);
            return TryObserveDefinition(
                factStore,
                PEGANpcKnowledgeFacts.PerceivedAreaPresence,
                (byte)0,
                value,
                evidence,
                out result,
                out diagnostic);
        }
    }

    [Serializable]
    [PEGAActorGroupInitialKnowledgeFactMenu("Contato visual de Actor")]
    public sealed class PEGAActorGroupInitialCharacterVisualContactFact : PEGAActorGroupInitialKnowledgeFact
    {
        private const string DescriptorSource = "PEGA.ActorGroup.InitialKnowledge";

        [SerializeField]
        [Tooltip("Actor ao qual se refere o fato inicial de contato visual deste Group.")]
        private ActorDeclaration subjectActor;

        [SerializeField]
        [Tooltip("Estado de contato visual autorado. Observações posteriores continuam na autoridade e no escopo de sua fonte.")]
        private bool hasVisualContact;

        internal override string FactName => PEGANpcKnowledgeFacts.CharacterVisualContact.Name;

        internal override bool TryObserve(
            PEGAKnowledgeFactStore factStore,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (subjectActor == null)
            {
                diagnostic = "An Actor Declaration is required to address the visual-contact fact.";
                return false;
            }

            if (!subjectActor.TryCreateDescriptor(
                    DescriptorSource,
                    out ActorDescriptor descriptor,
                    out ActorSetIssue issue))
            {
                diagnostic = issue.Message;
                return false;
            }

            return TryObserveDefinition(
                factStore,
                PEGANpcKnowledgeFacts.CharacterVisualContact,
                descriptor.ActorId,
                hasVisualContact,
                evidence,
                out result,
                out diagnostic);
        }
    }
}
