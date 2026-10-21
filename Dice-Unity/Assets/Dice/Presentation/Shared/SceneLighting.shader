Shader "Dice/Scene Lighting"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _BaseTex ("Ambient Base", 2D) = "black" {}
        _LightTex0 ("Light 0", 2D) = "black" {}
        _LightTex1 ("Light 1", 2D) = "black" {}
        _LightTex2 ("Light 2", 2D) = "black" {}
        _LightTex3 ("Light 3", 2D) = "black" {}
        _SceneLightResponseTex0 ("Light 0 Linear Response", 2D) = "black" {}
        _SceneLightResponseTex1 ("Light 1 Linear Response", 2D) = "black" {}
        _SceneLightResponseTex2 ("Light 2 Linear Response", 2D) = "black" {}
        _SceneLightResponseTex3 ("Light 3 Linear Response", 2D) = "black" {}
        _SceneLightResponseEnabled0 ("Light 0 Response Enabled", Float) = 0
        _SceneLightResponseEnabled1 ("Light 1 Response Enabled", Float) = 0
        _SceneLightResponseEnabled2 ("Light 2 Response Enabled", Float) = 0
        _SceneLightResponseEnabled3 ("Light 3 Response Enabled", Float) = 0
        _SceneLightDistanceFalloff0 ("Light 0 Distance Falloff", Range(0,4)) = 0
        _SceneLightDistanceFalloff1 ("Light 1 Distance Falloff", Range(0,4)) = 0
        _SceneLightDistanceFalloff2 ("Light 2 Distance Falloff", Range(0,4)) = 0
        _SceneLightDistanceFalloff3 ("Light 3 Distance Falloff", Range(0,4)) = 0
        _SceneLightSourceResponse0 ("Light 0 Source Response", Range(0.1,2)) = 1
        _SceneLightSourceResponse1 ("Light 1 Source Response", Range(0.1,2)) = 1
        _SceneLightSourceResponse2 ("Light 2 Source Response", Range(0.1,2)) = 1
        _SceneLightSourceResponse3 ("Light 3 Source Response", Range(0.1,2)) = 1
        _SceneLightConstrainAmbient0 ("Light 0 Constrains Ambient", Float) = 0
        _SceneLightConstrainAmbient1 ("Light 1 Constrains Ambient", Float) = 0
        _SceneLightConstrainAmbient2 ("Light 2 Constrains Ambient", Float) = 0
        _SceneLightConstrainAmbient3 ("Light 3 Constrains Ambient", Float) = 0
        _SceneLightUvOffset0 ("Light 0 UV Offset", Vector) = (0,0,0,0)
        _SceneLightUvOffset1 ("Light 1 UV Offset", Vector) = (0,0,0,0)
        _SceneLightUvOffset2 ("Light 2 UV Offset", Vector) = (0,0,0,0)
        _SceneLightUvOffset3 ("Light 3 UV Offset", Vector) = (0,0,0,0)
        _SceneLightModulateByBase0 ("Light 0 Modulates Base", Float) = 0
        _SceneLightModulateByBase1 ("Light 1 Modulates Base", Float) = 0
        _SceneLightModulateByBase2 ("Light 2 Modulates Base", Float) = 0
        _SceneLightModulateByBase3 ("Light 3 Modulates Base", Float) = 0
        _SceneLightTint0 ("Light 0 Linear Tint", Vector) = (1,1,1,1)
        _SceneLightTint1 ("Light 1 Linear Tint", Vector) = (1,1,1,1)
        _SceneLightTint2 ("Light 2 Linear Tint", Vector) = (1,1,1,1)
        _SceneLightTint3 ("Light 3 Linear Tint", Vector) = (1,1,1,1)
        _LightIntensity0 ("Light 0 Intensity", Float) = 0
        _LightIntensity1 ("Light 1 Intensity", Float) = 0
        _LightIntensity2 ("Light 2 Intensity", Float) = 0
        _LightIntensity3 ("Light 3 Intensity", Float) = 0
        _LightReference0 ("Light 0 Includes Base", Float) = 0
        _LightReference1 ("Light 1 Includes Base", Float) = 0
        _LightReference2 ("Light 2 Includes Base", Float) = 0
        _LightReference3 ("Light 3 Includes Base", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityUI.cginc"
        struct appdata
        {
            float4 vertex : POSITION;
            fixed4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct v2f
        {
            float4 vertex : SV_POSITION;
            fixed4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            float4 localPosition : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        sampler2D _BaseTex, _LightTex0, _LightTex1, _LightTex2, _LightTex3;
        sampler2D _SceneLightResponseTex0, _SceneLightResponseTex1, _SceneLightResponseTex2, _SceneLightResponseTex3;
        float4 _SceneLightTint0, _SceneLightTint1, _SceneLightTint2, _SceneLightTint3;
        float _LightIntensity0, _LightIntensity1, _LightIntensity2, _LightIntensity3;
        float _LightReference0, _LightReference1, _LightReference2, _LightReference3;
        float _SceneLightResponseEnabled0, _SceneLightResponseEnabled1, _SceneLightResponseEnabled2, _SceneLightResponseEnabled3;
        float _SceneLightDistanceFalloff0, _SceneLightDistanceFalloff1, _SceneLightDistanceFalloff2, _SceneLightDistanceFalloff3;
        float _SceneLightSourceResponse0, _SceneLightSourceResponse1, _SceneLightSourceResponse2, _SceneLightSourceResponse3;
        float _SceneLightConstrainAmbient0, _SceneLightConstrainAmbient1, _SceneLightConstrainAmbient2, _SceneLightConstrainAmbient3;
        float4 _SceneLightUvOffset0, _SceneLightUvOffset1, _SceneLightUvOffset2, _SceneLightUvOffset3;
        float _SceneLightModulateByBase0, _SceneLightModulateByBase1, _SceneLightModulateByBase2, _SceneLightModulateByBase3;
        fixed4 _Color;
        float4 _ClipRect;

        v2f vert(appdata input)
        {
            v2f output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.localPosition = input.vertex;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.texcoord = input.texcoord;
            output.color = input.color * _Color;
            return output;
        }

        float3 DecodeColor(float3 value)
        {
            #ifdef UNITY_COLORSPACE_GAMMA
            return GammaToLinearSpace(value);
            #else
            return value;
            #endif
        }

        float ResponseScale(float intensity, float2 response, float responseEnabled, float distanceFalloff, float sourceResponse)
        {
            if (intensity <= 0.0) return 0.0;
            if (responseEnabled < 0.5) return intensity;
            // Authored distance adds artistic low-power falloff; source pixels retain a separate glow response.
            float surfaceScale = intensity * pow(min(intensity, 1.0), saturate(response.r) * distanceFalloff);
            float sourceScale = pow(intensity, sourceResponse);
            return lerp(surfaceScale, sourceScale, saturate(response.g));
        }

        float InsideImage(float2 uv)
        {
            return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
        }

        float3 Contribution(float3 sampled, float3 ambient, float includesBase, float3 tint, float intensity, float modulate)
        {
            return max(sampled - ambient * includesBase, 0.0) * tint * intensity * lerp(float3(1,1,1), ambient, modulate);
        }

        float4 Compose(float2 uv)
        {
            float4 source = tex2D(_BaseTex, uv);
            float3 ambient = DecodeColor(source.rgb);
            float2 uv0 = uv - _SceneLightUvOffset0.xy;
            float2 uv1 = uv - _SceneLightUvOffset1.xy;
            float2 uv2 = uv - _SceneLightUvOffset2.xy;
            float2 uv3 = uv - _SceneLightUvOffset3.xy;
            float3 sampled0 = DecodeColor(tex2D(_LightTex0, uv0).rgb) * InsideImage(uv0);
            float3 sampled1 = DecodeColor(tex2D(_LightTex1, uv1).rgb) * InsideImage(uv1);
            float3 sampled2 = DecodeColor(tex2D(_LightTex2, uv2).rgb) * InsideImage(uv2);
            float3 sampled3 = DecodeColor(tex2D(_LightTex3, uv3).rgb) * InsideImage(uv3);
            // All references subtract the same ambient, constrained before any contribution is separated.
            if (_LightReference0 > 0.5 && _SceneLightConstrainAmbient0 > 0.5) ambient = min(ambient, sampled0);
            if (_LightReference1 > 0.5 && _SceneLightConstrainAmbient1 > 0.5) ambient = min(ambient, sampled1);
            if (_LightReference2 > 0.5 && _SceneLightConstrainAmbient2 > 0.5) ambient = min(ambient, sampled2);
            if (_LightReference3 > 0.5 && _SceneLightConstrainAmbient3 > 0.5) ambient = min(ambient, sampled3);
            float scale0 = ResponseScale(_LightIntensity0, tex2D(_SceneLightResponseTex0, uv0).rg, _SceneLightResponseEnabled0, _SceneLightDistanceFalloff0, _SceneLightSourceResponse0);
            float scale1 = ResponseScale(_LightIntensity1, tex2D(_SceneLightResponseTex1, uv1).rg, _SceneLightResponseEnabled1, _SceneLightDistanceFalloff1, _SceneLightSourceResponse1);
            float scale2 = ResponseScale(_LightIntensity2, tex2D(_SceneLightResponseTex2, uv2).rg, _SceneLightResponseEnabled2, _SceneLightDistanceFalloff2, _SceneLightSourceResponse2);
            float scale3 = ResponseScale(_LightIntensity3, tex2D(_SceneLightResponseTex3, uv3).rg, _SceneLightResponseEnabled3, _SceneLightDistanceFalloff3, _SceneLightSourceResponse3);
            float3 light = Contribution(sampled0, ambient, _LightReference0, _SceneLightTint0.rgb, scale0, _SceneLightModulateByBase0);
            light += Contribution(sampled1, ambient, _LightReference1, _SceneLightTint1.rgb, scale1, _SceneLightModulateByBase1);
            light += Contribution(sampled2, ambient, _LightReference2, _SceneLightTint2.rgb, scale2, _SceneLightModulateByBase2);
            light += Contribution(sampled3, ambient, _LightReference3, _SceneLightTint3.rgb, scale3, _SceneLightModulateByBase3);
            float3 color = ambient + light;
            #ifdef UNITY_COLORSPACE_GAMMA
            color = LinearToGammaSpace(color);
            #endif
            return float4(color, source.a);
        }

        float4 frag(v2f input) : SV_Target
        {
            float4 color = Compose(input.texcoord) * input.color;
            #ifdef UNITY_UI_CLIP_RECT
            color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
            #endif
            #ifdef UNITY_UI_ALPHACLIP
            clip(color.a - 0.001);
            #endif
            return color;
        }

        float4 fragPreview(v2f input) : SV_Target
        {
            return Compose(input.texcoord);
        }
        ENDCG

        Pass
        {
            Name "Scene Lighting UI"
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            ENDCG
        }
        Pass
        {
            Name "Scene Lighting Preview"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPreview
            #pragma target 3.0
            ENDCG
        }
    }
}
