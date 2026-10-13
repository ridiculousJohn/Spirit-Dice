Shader "Dice/Spirit Smoke"
{
    Properties
    {
        [PerRendererData] _MainTex ("Smoke", 2D) = "white" {}
        _MotionMask ("Motion Mask", 2D) = "white" {}
        _NoiseTex ("Flow Noise RG", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EffectTime ("Time", Float) = 0
        _Amount ("Distortion", Range(0,0.05)) = 0.012
        _Speed ("Noise Speed", Float) = 0.15
        _Flow ("Flow Direction", Vector) = (0,0.018,0,0)
        _Opacity ("Opacity", Range(0,1)) = 1
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityUI.cginc"
        sampler2D _MainTex, _MotionMask, _NoiseTex;
        float4 _MainTex_ST, _ClipRect, _Flow;
        float4 _Color;
        float _EffectTime, _Amount, _Speed, _Opacity;
        struct appdata
        {
            float4 vertex : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };
        struct v2f
        {
            float4 vertex : SV_POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            float4 localPosition : TEXCOORD1;
        };
        v2f vert(appdata input)
        {
            v2f output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.localPosition = input.vertex;
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color * _Color;
            return output;
        }
        float4 Smoke(float2 uv)
        {
            float motion = saturate(tex2D(_MotionMask, uv).r);
            float time = _EffectTime * _Speed;
            float2 drift = _Flow.xy * time;
            float2 coarse = tex2D(_NoiseTex, uv * 1.7 - drift * 4.0).rg * 2.0 - 1.0;
            float2 fine = tex2D(_NoiseTex, uv * 4.3 - drift * 7.0 + float2(0.37,0.61)).gr * 2.0 - 1.0;
            // The mask fixes the attachment edge while the outer vapor deforms and thins.
            float2 offset = (coarse * 1.5 + fine * 0.5) * (_Amount * motion);
            float2 sampleUv = uv + offset;
            float4 color = tex2D(_MainTex, sampleUv);
            float inside = step(0, sampleUv.x) * step(sampleUv.x, 1) * step(0, sampleUv.y) * step(sampleUv.y, 1);
            float density = tex2D(_NoiseTex, uv * 3.1 - drift * 5.0 + coarse * 0.15).r;
            color.a *= inside * saturate(_Opacity) * lerp(1.0, smoothstep(0.12, 0.72, density), motion);
            return color;
        }
        float4 frag(v2f input) : SV_Target
        {
            float4 color = Smoke(input.uv) * input.color;
            #ifdef UNITY_UI_CLIP_RECT
            color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
            #endif
            #ifdef UNITY_UI_ALPHACLIP
            clip(color.a - 0.001);
            #endif
            return color;
        }
        float4 fragPreview(v2f input) : SV_Target { return Smoke(input.uv) * _Color; }
        ENDCG
        Pass
        {
            Name "Spirit Smoke UI"
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
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
            Name "Spirit Smoke Preview"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPreview
            #pragma target 3.0
            ENDCG
        }
    }
}
