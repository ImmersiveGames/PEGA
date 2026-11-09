using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.Presentation;
using UnityEngine;

namespace PEGA.Player
{
    /// <summary>Projeta a seleção existente do Player nos componentes visuais presentes na ocorrência.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGAPlayerObjectEntrySelectionSource))]
    [AddComponentMenu("PEGA/Player/Selection Feedback Presenter")]
    public sealed class PEGAPlayerSelectionFeedbackPresenter : MonoBehaviour
    {
        private readonly List<PEGASelectionOutlineFeedback> _activeFeedback =
            new List<PEGASelectionOutlineFeedback>();
        private readonly List<Transform> _excludedSubtrees = new List<Transform>();

        private PEGAPlayerObjectEntrySelectionSource _selectionSource;

        private void Awake()
        {
            _selectionSource = GetComponent<PEGAPlayerObjectEntrySelectionSource>();
        }

        private void OnEnable()
        {
            if (_selectionSource == null)
            {
                _selectionSource = GetComponent<PEGAPlayerObjectEntrySelectionSource>();
            }

            if (_selectionSource == null)
            {
                enabled = false;
                return;
            }

            _selectionSource.SelectionChanged += HandleSelectionChanged;
            HandleSelectionChanged(_selectionSource.CurrentSelection);
        }

        private void OnDisable()
        {
            if (_selectionSource != null)
            {
                _selectionSource.SelectionChanged -= HandleSelectionChanged;
            }

            ClearActiveFeedback();
        }

        private void HandleSelectionChanged(PEGAPlayerObjectEntryCandidate? selection)
        {
            ClearActiveFeedback();
            _excludedSubtrees.Clear();
            if (!selection.HasValue)
            {
                return;
            }

            GameObject occurrence = selection.Value.Occurrence;
            if (occurrence == null || !occurrence.activeInHierarchy)
            {
                return;
            }

            CollectFeedbackWithinOccurrence(occurrence.transform, isSelectedRoot: true);
            ActivateCollectedFeedback(occurrence.transform);
        }

        private void CollectFeedbackWithinOccurrence(Transform current, bool isSelectedRoot = false)
        {
            if (current == null || !current.gameObject.activeInHierarchy)
            {
                return;
            }

            if (!isSelectedRoot && HasPhysicalDeclaration(current))
            {
                _excludedSubtrees.Add(current);
                return;
            }

            PEGASelectionOutlineFeedback[] feedbackComponents =
                current.GetComponents<PEGASelectionOutlineFeedback>();
            for (int index = 0; index < feedbackComponents.Length; index++)
            {
                PEGASelectionOutlineFeedback feedback = feedbackComponents[index];
                if (feedback != null && feedback.isActiveAndEnabled)
                {
                    _activeFeedback.Add(feedback);
                }
            }

            for (int index = 0; index < current.childCount; index++)
            {
                CollectFeedbackWithinOccurrence(current.GetChild(index));
            }
        }

        private void ActivateCollectedFeedback(Transform selectedOccurrenceRoot)
        {
            for (int index = 0; index < _activeFeedback.Count; index++)
            {
                PEGASelectionOutlineFeedback feedback = _activeFeedback[index];
                if (feedback != null)
                {
                    feedback.SetSelected(
                        true,
                        selectedOccurrenceRoot,
                        _excludedSubtrees);
                }
            }
        }

        private static bool HasPhysicalDeclaration(Transform target) =>
            target.GetComponent<ActorDeclaration>() != null ||
            target.GetComponent<ObjectEntryDeclaration>() != null;

        private void ClearActiveFeedback()
        {
            for (int index = 0; index < _activeFeedback.Count; index++)
            {
                PEGASelectionOutlineFeedback feedback = _activeFeedback[index];
                if (feedback != null)
                {
                    feedback.SetSelected(false);
                }
            }

            _activeFeedback.Clear();
        }
    }
}
