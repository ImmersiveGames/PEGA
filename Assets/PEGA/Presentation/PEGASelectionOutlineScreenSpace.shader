Shader "PEGA/Selection Outline Screen Space"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.58, 0.08, 1)
        _OutlineThicknessPixels ("Outline Thickness (pixels)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SelectionOutlineComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_PEGASelectionMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThicknessPixels;
                float4 _PEGASelectionMask_TexelSize;
                float4 _PEGASelectionMaskToOutputScale;
            CBUFFER_END

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(
                    _PEGASelectionMask,
                    sampler_PointClamp,
                    uv).r;
            }

            half FindExternalOutline(float2 uv)
            {
                half center = SampleMask(uv);
                if (center > 0.5h)
                {
                    return 0.0h;
                }

                float2 pixelStep = _PEGASelectionMask_TexelSize.xy *
                    _PEGASelectionMaskToOutputScale.xy;
                int radius = (int)ceil(_OutlineThicknessPixels);
                [loop]
                for (int stepIndex = 1; stepIndex <= 8; stepIndex++)
                {
                    if (stepIndex > radius)
                    {
                        break;
                    }

                    float distancePixels = min((float)stepIndex, _OutlineThicknessPixels);
                    float2 offset = pixelStep * distancePixels;
                    half selected = 0.0h;
                    selected = max(selected, SampleMask(uv + float2( offset.x, 0.0)));
                    selected = max(selected, SampleMask(uv + float2(-offset.x, 0.0)));
                    selected = max(selected, SampleMask(uv + float2(0.0,  offset.y)));
                    selected = max(selected, SampleMask(uv + float2(0.0, -offset.y)));
                    selected = max(selected, SampleMask(uv + float2( offset.x * 0.70710678,  offset.y * 0.70710678)));
                    selected = max(selected, SampleMask(uv + float2(-offset.x * 0.70710678,  offset.y * 0.70710678)));
                    selected = max(selected, SampleMask(uv + float2( offset.x * 0.70710678, -offset.y * 0.70710678)));
                    selected = max(selected, SampleMask(uv + float2(-offset.x * 0.70710678, -offset.y * 0.70710678)));
                    if (selected > 0.5h)
                    {
                        return 1.0h;
                    }
                }

                return 0.0h;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half outline = FindExternalOutline(input.texcoord);
                return half4(_OutlineColor.rgb, _OutlineColor.a * outline);
            }
            ENDHLSL
        }
    }
}
