using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PEGA.Presentation
{
    /// <summary>Desenha a máscara dos renderers selecionados e compõe o contorno screen-space.</summary>
    public sealed class PegaSelectionOutlineRendererFeature : ScriptableRendererFeature
    {
        private const string LogCategory = "PEGA.Presentation.SelectionOutlineRenderer";
        private const int MaximumOutlineThicknessPixels = 8;

        [SerializeField, Tooltip("Material opaco usado para escrever os renderers selecionados na máscara R8.")]
        private Material maskMaterial;

        [SerializeField, Tooltip("Material URP que expande a máscara e compõe o contorno sobre a cor ativa.")]
        private Material compositeMaterial;

        [SerializeField, ColorUsage(true, true), Tooltip("Cor HDR do contorno screen-space compartilhada pelas instâncias.")]
        private Color outlineColor = new Color(1f, 0.58f, 0.08f, 1f);

        [SerializeField, Range(1f, MaximumOutlineThicknessPixels), Tooltip("Espessura medida em pixels da imagem final, compensada pela escala do alvo de renderização.")]
        private float outlineThicknessPixels = 3f;

        private SelectionOutlinePass _pass;
        private ScopedLogger _log;
        private bool _configurationErrorReported;
        private bool _formatErrorReported;
        private bool _msaaErrorReported;

        public override void Create()
        {
            _pass ??= new SelectionOutlinePass();
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        private void OnValidate()
        {
            if (float.IsNaN(outlineThicknessPixels) || float.IsInfinity(outlineThicknessPixels))
            {
                outlineThicknessPixels = 3f;
            }
            else if (outlineThicknessPixels < 1f)
            {
                outlineThicknessPixels = 1f;
            }
            else if (outlineThicknessPixels > MaximumOutlineThicknessPixels)
            {
                outlineThicknessPixels = MaximumOutlineThicknessPixels;
            }
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (PegaSelectionOutlineMaskRegistry.ActiveRendererCount == 0 ||
                renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            if (maskMaterial == null || compositeMaterial == null ||
                maskMaterial.passCount == 0 || compositeMaterial.passCount == 0 ||
                !IsValidColor(outlineColor) ||
                outlineThicknessPixels < 1f ||
                outlineThicknessPixels > MaximumOutlineThicknessPixels)
            {
                LogConfigurationError("invalid-material-or-settings");
                return;
            }

            if (renderingData.cameraData.cameraTargetDescriptor.msaaSamples != 1)
            {
                if (!_msaaErrorReported)
                {
                    _msaaErrorReported = true;
                    GetLogger().Error(
                        "Screen-space selection outline requires a single-sample camera target.",
                        LogFields.Field("msaaSamples", renderingData.cameraData.cameraTargetDescriptor.msaaSamples),
                        LogFields.Field("reason", "multisampled-camera-target-unsupported"));
                }

                return;
            }

            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render))
            {
                if (!_formatErrorReported)
                {
                    _formatErrorReported = true;
                    GetLogger().Error(
                        "Screen-space selection outline requires an R8 render target on this graphics device.",
                        LogFields.Field("format", GraphicsFormat.R8_UNorm),
                        LogFields.Field("reason", "r8-render-target-unsupported"));
                }

                return;
            }

            _pass.requiresIntermediateTexture = true;
            _pass.Setup(maskMaterial, compositeMaterial, outlineColor, outlineThicknessPixels, GetLogger());
            renderer.EnqueuePass(_pass);
        }

        private void LogConfigurationError(string reason)
        {
            if (_configurationErrorReported)
            {
                return;
            }

            _configurationErrorReported = true;
            GetLogger().Error(
                "Screen-space selection outline is unavailable because its renderer feature configuration is invalid.",
                LogFields.Field("hasMaskMaterial", maskMaterial != null),
                LogFields.Field("hasCompositeMaterial", compositeMaterial != null),
                LogFields.Field("outlineThicknessPixels", outlineThicknessPixels),
                LogFields.Field("reason", reason));
        }

        private ScopedLogger GetLogger()
        {
            _log ??= PegaLogging.CreateScopedLogger<PegaSelectionOutlineRendererFeature>(
                LogCategory,
                name);
            return _log;
        }

        private static bool IsValidColor(Color value) =>
            IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) &&
            IsFinite(value.a) && value.a > 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class SelectionOutlinePass : ScriptableRenderPass
        {
            private static readonly int OutlineMaskId = Shader.PropertyToID("_PEGASelectionMask");
            private static readonly int OutlineMaskTexelSizeId = Shader.PropertyToID("_PEGASelectionMask_TexelSize");
            private static readonly int MaskToOutputScaleId = Shader.PropertyToID("_PEGASelectionMaskToOutputScale");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineThicknessPixelsId = Shader.PropertyToID("_OutlineThicknessPixels");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

            private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalGBuffer"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("Universal2D")
            };

            private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

            private Material _maskMaterial;
            private Material _compositeMaterial;
            private Color _outlineColor;
            private float _outlineThicknessPixels;
            private ScopedLogger _log;
            private bool _targetErrorReported;

            internal void Setup(
                Material maskMaterial,
                Material compositeMaterial,
                Color color,
                float thicknessPixels,
                ScopedLogger log)
            {
                _maskMaterial = maskMaterial;
                _compositeMaterial = compositeMaterial;
                _outlineColor = color;
                _outlineThicknessPixels = thicknessPixels;
                _log = log;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                if (!resources.activeColorTexture.IsValid() || !resources.activeDepthTexture.IsValid())
                {
                    if (!_targetErrorReported)
                    {
                        _targetErrorReported = true;
                        _log.Error(
                            "Screen-space selection outline requires valid color and depth attachments.",
                            LogFields.Field("hasColorAttachment", resources.activeColorTexture.IsValid()),
                            LogFields.Field("hasDepthAttachment", resources.activeDepthTexture.IsValid()),
                            LogFields.Field("reason", "camera-attachments-unavailable"));
                    }

                    return;
                }

                TextureDesc maskDescriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                maskDescriptor.name = "PEGA Selection Outline Mask";
                maskDescriptor.colorFormat = GraphicsFormat.R8_UNorm;
                maskDescriptor.depthBufferBits = DepthBits.None;
                maskDescriptor.msaaSamples = MSAASamples.None;
                maskDescriptor.bindTextureMS = false;
                maskDescriptor.useMipMap = false;
                maskDescriptor.filterMode = FilterMode.Point;
                maskDescriptor.clearBuffer = true;
                maskDescriptor.clearColor = Color.clear;

                TextureHandle mask = renderGraph.CreateTexture(maskDescriptor);
                RendererListHandle selectedRenderers = CreateSelectedRendererList(
                    renderGraph,
                    renderingData,
                    cameraData,
                    lightData);

                if (!selectedRenderers.IsValid())
                {
                    return;
                }

                RecordMaskPass(renderGraph, resources, mask, selectedRenderers);
                RecordCompositePass(
                    renderGraph,
                    resources,
                    cameraData,
                    mask,
                    maskDescriptor.width,
                    maskDescriptor.height);
            }

            private RendererListHandle CreateSelectedRendererList(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData)
            {
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTagIds,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = _maskMaterial;
                drawingSettings.overrideMaterialPassIndex = 0;

                var filteringSettings = new FilteringSettings(
                    RenderQueueRange.all,
                    ~0,
                    PegaSelectionOutlineMaskRegistry.SelectionRenderingLayerMask);
                var parameters = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings);
                return renderGraph.CreateRendererList(parameters);
            }

            private static void RecordMaskPass(
                RenderGraph renderGraph,
                UniversalResourceData resources,
                TextureHandle mask,
                RendererListHandle selectedRenderers)
            {
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "PEGA Selection Outline Mask",
                           out MaskPassData passData))
                {
                    passData.renderers = selectedRenderers;
                    builder.UseRendererList(passData.renderers);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(data.renderers));
                }
            }

            private void RecordCompositePass(
                RenderGraph renderGraph,
                UniversalResourceData resources,
                UniversalCameraData cameraData,
                TextureHandle mask,
                int maskWidth,
                int maskHeight)
            {
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "PEGA Selection Outline Composite",
                           out CompositePassData passData))
                {
                    passData.mask = mask;
                    passData.material = _compositeMaterial;
                    passData.color = _outlineColor;
                    passData.thicknessPixels = _outlineThicknessPixels;
                    passData.outputScale = new Vector2(
                        maskWidth / (float)Mathf.Max(1, cameraData.camera.pixelWidth),
                        maskHeight / (float)Mathf.Max(1, cameraData.camera.pixelHeight));

                    builder.UseTexture(passData.mask, AccessFlags.Read);
                    builder.SetRenderAttachment(
                        resources.activeColorTexture,
                        0,
                        AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                        DrawComposite(data, context));
                }
            }

            private static void DrawComposite(CompositePassData data, RasterGraphContext context)
            {
                Texture mask = data.mask;
                if (mask == null)
                {
                    return;
                }

                Properties.Clear();
                Properties.SetTexture(OutlineMaskId, mask);
                Properties.SetVector(
                    OutlineMaskTexelSizeId,
                    new Vector4(1f / mask.width, 1f / mask.height, mask.width, mask.height));
                Properties.SetVector(
                    MaskToOutputScaleId,
                    new Vector4(data.outputScale.x, data.outputScale.y, 0f, 0f));
                Properties.SetColor(OutlineColorId, data.color);
                Properties.SetFloat(OutlineThicknessPixelsId, data.thicknessPixels);
                Properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));

                context.cmd.DrawProcedural(
                    Matrix4x4.identity,
                    data.material,
                    0,
                    MeshTopology.Triangles,
                    3,
                    1,
                    Properties);
            }

            private sealed class MaskPassData
            {
                internal RendererListHandle renderers;
            }

            private sealed class CompositePassData
            {
                internal TextureHandle mask;
                internal Material material;
                internal Color color;
                internal float thicknessPixels;
                internal Vector2 outputScale;
            }
        }
    }
}
