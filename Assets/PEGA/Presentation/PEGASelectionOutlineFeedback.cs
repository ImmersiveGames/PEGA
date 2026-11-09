using System.Collections.Generic;
using System.Threading;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Presentation
{
    /// <summary>Exibe o contorno Screen Space enquanto um consumidor autoritativo marca a seleção.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Presentation/Selection Outline Feedback")]
    public sealed class PEGASelectionOutlineFeedback : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Presentation.SelectionOutline";
        private const string ScreenSpaceOutlineShaderName = "PEGA/Selection Outline Composite";

        private enum TargetRootMode
        {
            Self,
            Parent,
            ExplicitReference
        }

        [Header("Target")]
        [SerializeField]
        [Tooltip("Raiz visual que receberá o contorno. Self usa este objeto; Parent usa o pai direto (partes compostas); Explicit Reference usa Target Root.")]
        private TargetRootMode targetRootMode = TargetRootMode.Self;

        [SerializeField]
        [Tooltip("Raiz visual usada somente quando Target Root Mode está em Explicit Reference.")]
        private Transform targetRoot;

        private readonly List<Renderer> _screenSpaceRenderers = new List<Renderer>();
        private readonly List<Transform> _excludedSubtrees = new List<Transform>();
        private ScopedLogger _log;
        private bool _isSelected;
        private bool _targetRootErrorReported;
        private bool _targetScopeErrorReported;
        private bool _noRendererWarningReported;
        private bool _hasSelectionScope;
        private int _outlineRefreshRequested;
        private Transform _selectionScope;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGASelectionOutlineFeedback>(
                LogCategory,
                gameObject.name);
        }

        private void OnEnable()
        {
            if (ResolveTargetRoot() == null)
            {
                if (!_targetRootErrorReported)
                {
                    _targetRootErrorReported = true;
                    _log.Error(
                        "Selection outline target root is not available for the configured target mode.",
                        LogFields.Field("targetRootMode", targetRootMode),
                        LogFields.Field("reason", "selection-outline-target-root-unavailable"));
                }

                enabled = false;
                return;
            }

            if (_isSelected)
            {
                if (!IsTargetWithinSelectionScope())
                {
                    _isSelected = false;
                    _hasSelectionScope = false;
                    _selectionScope = null;
                    return;
                }

                ApplySelectedOutline();
            }
        }

        private void OnDisable() => ClearActiveOutline();

        private void OnDestroy() => ClearActiveOutline();

        private void Update()
        {
            if (Interlocked.Exchange(ref _outlineRefreshRequested, 0) == 0)
            {
                return;
            }

            if (!isActiveAndEnabled || !_isSelected || !EnsureTargetWithinSelectionScope())
            {
                return;
            }

            ApplySelectedOutline();
        }

        private void OnValidate() => Interlocked.Exchange(ref _outlineRefreshRequested, 1);

        /// <summary>Define se o contorno está visível; a decisão de seleção pertence ao consumidor.</summary>
        public void SetSelected(bool selected) => SetSelected(selected, null, null);

        /// <summary>Define a seleção, limita a raiz visual à ocorrência indicada e exclui outras subárvores.</summary>
        public void SetSelected(
            bool selected,
            Transform selectionScope,
            IReadOnlyList<Transform> excludedSubtrees)
        {
            bool scopeChanged = selected && _isSelected &&
                (!object.ReferenceEquals(_selectionScope, selectionScope) ||
                 !HasSameExcludedSubtrees(excludedSubtrees));
            if (scopeChanged)
            {
                ClearActiveOutline();
            }

            _isSelected = selected;
            _hasSelectionScope = selected && selectionScope != null;
            _selectionScope = _hasSelectionScope ? selectionScope : null;
            _excludedSubtrees.Clear();
            if (selected && excludedSubtrees != null)
            {
                for (int index = 0; index < excludedSubtrees.Count; index++)
                {
                    Transform excluded = excludedSubtrees[index];
                    if (excluded != null && !_excludedSubtrees.Contains(excluded))
                    {
                        _excludedSubtrees.Add(excluded);
                    }
                }
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!selected)
            {
                ClearActiveOutline();
                return;
            }

            if (!EnsureTargetWithinSelectionScope())
            {
                return;
            }

            ApplySelectedOutline();
        }

        private void ApplySelectedOutline()
        {
            ClearActiveOutline();
            RegisterScreenSpaceRenderers();
            if (_screenSpaceRenderers.Count == 0)
            {
                LogNoSupportedRenderers("no-screen-space-renderer-found");
            }
        }

        private void RegisterScreenSpaceRenderers()
        {
            Transform root = ResolveTargetRoot();
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer source = renderers[index];
                if (source == null ||
                    source.gameObject.hideFlags.HasFlag(HideFlags.HideAndDontSave) ||
                    UsesOutlineShader(source) ||
                    IsInExcludedSubtree(source.transform) ||
                    !source.enabled ||
                    source.forceRenderingOff ||
                    _screenSpaceRenderers.Contains(source))
                {
                    continue;
                }

                PEGASelectionOutlineMaskRegistry.Acquire(source);
                _screenSpaceRenderers.Add(source);
            }
        }

        private void LogNoSupportedRenderers(string reason)
        {
            if (_noRendererWarningReported)
            {
                return;
            }

            _noRendererWarningReported = true;
            Transform root = ResolveTargetRoot();
            _log.Warning(
                "Selection outline has no supported renderers under its target root.",
                LogFields.Field("targetRoot", root != null ? root.name : string.Empty),
                LogFields.Field("reason", reason));
        }

        private void ClearActiveOutline()
        {
            for (int index = 0; index < _screenSpaceRenderers.Count; index++)
            {
                PEGASelectionOutlineMaskRegistry.Release(_screenSpaceRenderers[index]);
            }

            _screenSpaceRenderers.Clear();
        }

        private Transform ResolveTargetRoot()
        {
            switch (targetRootMode)
            {
                case TargetRootMode.Self:
                    return transform;
                case TargetRootMode.Parent:
                    return transform.parent;
                case TargetRootMode.ExplicitReference:
                    return targetRoot;
                default:
                    return null;
            }
        }

        private bool IsInExcludedSubtree(Transform candidate)
        {
            for (int index = 0; index < _excludedSubtrees.Count; index++)
            {
                Transform excluded = _excludedSubtrees[index];
                if (excluded != null &&
                    (candidate == excluded || candidate.IsChildOf(excluded)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTargetWithinSelectionScope()
        {
            if (!_hasSelectionScope)
            {
                return true;
            }

            Transform root = ResolveTargetRoot();
            if (root == null || _selectionScope == null ||
                (root != _selectionScope && !root.IsChildOf(_selectionScope)))
            {
                return false;
            }

            return !IsInExcludedSubtree(root);
        }

        private bool EnsureTargetWithinSelectionScope()
        {
            if (IsTargetWithinSelectionScope())
            {
                return true;
            }

            if (!_targetScopeErrorReported)
            {
                _targetScopeErrorReported = true;
                Transform resolvedRoot = ResolveTargetRoot();
                _log.Warning(
                    "Selection outline target is outside the selected occurrence and was ignored.",
                    LogFields.Field("targetRoot", resolvedRoot != null ? resolvedRoot.name : string.Empty),
                    LogFields.Field("selectionScope", _selectionScope != null ? _selectionScope.name : string.Empty),
                    LogFields.Field("reason", "outline-target-outside-selection-scope"));
            }

            _isSelected = false;
            _hasSelectionScope = false;
            _selectionScope = null;
            ClearActiveOutline();
            return false;
        }

        private bool HasSameExcludedSubtrees(IReadOnlyList<Transform> excludedSubtrees)
        {
            int validCount = 0;
            for (int index = 0; excludedSubtrees != null && index < excludedSubtrees.Count; index++)
            {
                if (excludedSubtrees[index] == null)
                {
                    continue;
                }

                validCount++;
                if (!_excludedSubtrees.Contains(excludedSubtrees[index]))
                {
                    return false;
                }
            }

            return validCount == _excludedSubtrees.Count;
        }

        private bool UsesOutlineShader(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null && materials[index].shader != null &&
                    materials[index].shader.name == ScreenSpaceOutlineShaderName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
