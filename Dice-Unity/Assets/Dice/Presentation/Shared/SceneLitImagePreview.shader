Shader "Hidden/Dice/Scene Lit Image Preview"
{
    Properties
    {
        [PerRendererData] _MainTex ("Image Texture", 2D) = "white" {}
        _SourceTex ("Unlit Base Texture", 2D) = "white" {}
        _LayerMask ("Layer Selection Mask", 2D) = "black" {}
        _InvertMask ("Invert Layer Selection", Range(0,1)) = 0
        _SceneLitMode0 ("Light 0 Mode (0 Off, 1 Reference, 2 Computed)", Float) = 0
        _SceneLitReferenceTex0 ("Light 0 Lit Reference", 2D) = "black" {}
        _SceneLitResponseTex0 ("Light 0 Linear Response", 2D) = "black" {}
        _SceneLitResponseEnabled0 ("Light 0 Response Enabled", Float) = 0
        _SceneLitStrength0 ("Light 0 Computed Strength", Float) = 1
        _SceneLitIntensity0 ("Light 0 Intensity", Float) = 0
        _SceneLitTint0 ("Light 0 Linear Tint", Vector) = (1,1,1,1)
        _SceneLitDistanceFalloff0 ("Light 0 Distance Falloff", Range(0,4)) = 0
        _SceneLitSourceResponse0 ("Light 0 Source Response", Range(0.1,2)) = 1
        _SceneLitIncludesBase0 ("Light 0 Reference Includes Base", Float) = 1
        _SceneLitConstrainAmbient0 ("Light 0 Constrains Ambient", Float) = 0
        _SceneLitModulateByBase0 ("Light 0 Modulates Base", Float) = 0
        _SceneLitPosition0 ("Light 0 Rig Position And Range", Vector) = (0,0,0,1)
        _SceneLitColor0 ("Light 0 Linear Color And Receiver Intensity", Vector) = (0,0,0,0)
        _SceneLitMode1 ("Light 1 Mode (0 Off, 1 Reference, 2 Computed)", Float) = 0
        _SceneLitReferenceTex1 ("Light 1 Lit Reference", 2D) = "black" {}
        _SceneLitResponseTex1 ("Light 1 Linear Response", 2D) = "black" {}
        _SceneLitResponseEnabled1 ("Light 1 Response Enabled", Float) = 0
        _SceneLitStrength1 ("Light 1 Computed Strength", Float) = 1
        _SceneLitIntensity1 ("Light 1 Intensity", Float) = 0
        _SceneLitTint1 ("Light 1 Linear Tint", Vector) = (1,1,1,1)
        _SceneLitDistanceFalloff1 ("Light 1 Distance Falloff", Range(0,4)) = 0
        _SceneLitSourceResponse1 ("Light 1 Source Response", Range(0.1,2)) = 1
        _SceneLitIncludesBase1 ("Light 1 Reference Includes Base", Float) = 1
        _SceneLitConstrainAmbient1 ("Light 1 Constrains Ambient", Float) = 0
        _SceneLitModulateByBase1 ("Light 1 Modulates Base", Float) = 0
        _SceneLitPosition1 ("Light 1 Rig Position And Range", Vector) = (0,0,0,1)
        _SceneLitColor1 ("Light 1 Linear Color And Receiver Intensity", Vector) = (0,0,0,0)
        _SceneLitMode2 ("Light 2 Mode (0 Off, 1 Reference, 2 Computed)", Float) = 0
        _SceneLitReferenceTex2 ("Light 2 Lit Reference", 2D) = "black" {}
        _SceneLitResponseTex2 ("Light 2 Linear Response", 2D) = "black" {}
        _SceneLitResponseEnabled2 ("Light 2 Response Enabled", Float) = 0
        _SceneLitStrength2 ("Light 2 Computed Strength", Float) = 1
        _SceneLitIntensity2 ("Light 2 Intensity", Float) = 0
        _SceneLitTint2 ("Light 2 Linear Tint", Vector) = (1,1,1,1)
        _SceneLitDistanceFalloff2 ("Light 2 Distance Falloff", Range(0,4)) = 0
        _SceneLitSourceResponse2 ("Light 2 Source Response", Range(0.1,2)) = 1
        _SceneLitIncludesBase2 ("Light 2 Reference Includes Base", Float) = 1
        _SceneLitConstrainAmbient2 ("Light 2 Constrains Ambient", Float) = 0
        _SceneLitModulateByBase2 ("Light 2 Modulates Base", Float) = 0
        _SceneLitPosition2 ("Light 2 Rig Position And Range", Vector) = (0,0,0,1)
        _SceneLitColor2 ("Light 2 Linear Color And Receiver Intensity", Vector) = (0,0,0,0)
        _SceneLitMode3 ("Light 3 Mode (0 Off, 1 Reference, 2 Computed)", Float) = 0
        _SceneLitReferenceTex3 ("Light 3 Lit Reference", 2D) = "black" {}
        _SceneLitResponseTex3 ("Light 3 Linear Response", 2D) = "black" {}
        _SceneLitResponseEnabled3 ("Light 3 Response Enabled", Float) = 0
        _SceneLitStrength3 ("Light 3 Computed Strength", Float) = 1
        _SceneLitIntensity3 ("Light 3 Intensity", Float) = 0
        _SceneLitTint3 ("Light 3 Linear Tint", Vector) = (1,1,1,1)
        _SceneLitDistanceFalloff3 ("Light 3 Distance Falloff", Range(0,4)) = 0
        _SceneLitSourceResponse3 ("Light 3 Source Response", Range(0.1,2)) = 1
        _SceneLitIncludesBase3 ("Light 3 Reference Includes Base", Float) = 1
        _SceneLitConstrainAmbient3 ("Light 3 Constrains Ambient", Float) = 0
        _SceneLitModulateByBase3 ("Light 3 Modulates Base", Float) = 0
        _SceneLitPosition3 ("Light 3 Rig Position And Range", Vector) = (0,0,0,1)
        _SceneLitColor3 ("Light 3 Linear Color And Receiver Intensity", Vector) = (0,0,0,0)
        _SceneLitDepthTex ("Linear Depth", 2D) = "black" {}
        _SceneLitDepthRange ("Rig Z At Depth 0 And 1", Vector) = (0,-1,0,0)
        _SceneLitFrameOrigin ("Frame Origin", Vector) = (0,0,0,0)
        _SceneLitFrameAxisX ("Frame Axis X", Vector) = (1,0,0,0)
        _SceneLitFrameAxisY ("Frame Axis Y", Vector) = (0,1,0,0)
        _SceneLitFrameTransform ("Texture To Frame UV", Vector) = (1,1,0,0)
        _SceneLitMapRect ("Response And Depth Map Rect", Vector) = (0,0,1,1)
        _SceneLitWrap ("Computed Light Wrap", Range(0,1)) = 1
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    // The lit composition without vertex color, alpha or layer mask, opaque, for blits from a copy of a lit layer material.
    // A separate shader because UGUI draws every pass of a material's shader.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "PreviewType" = "Plane" }
        Pass
        {
            Name "Scene Lit Image Preview"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off
            CGPROGRAM
            #include "SceneLitImage.cginc"
            #pragma vertex vert
            #pragma fragment fragPreview
            #pragma target 3.0
            ENDCG
        }
    }
}
