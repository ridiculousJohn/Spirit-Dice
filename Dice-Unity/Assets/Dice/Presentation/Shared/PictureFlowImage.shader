Shader "Dice/Picture Flow Image"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _WindowTex ("Window", 2D) = "black" {}
        _FlowTex ("Flow", 2D) = "gray" {}
        _FlowRect ("Flow Rect (picture UV x, y, width, height)", Vector) = (0,0,1,1)
        _FlowSpan ("Displacement at a phase's end (picture UV)", Vector) = (0,0,0,0)
        _FlowPhase ("Phase", Range(0,1)) = 0
        _NoiseStrength ("Phase Noise", Range(0,1)) = 0
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Picture Flow Image UI"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
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
            sampler2D _MainTex;
            sampler2D _WindowTex;
            sampler2D _FlowTex;
            float4 _FlowRect;
            float4 _FlowSpan;
            float _FlowPhase;
            float _NoiseStrength;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                // The image's UV rect is the layer's rectangle, so these are the picture's own texture coordinates.
                output.texcoord = input.texcoord;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0 / alphaPrecision);
                input.color.a = round(input.color.a * alphaPrecision) * invAlphaPrecision;
                float2 uv = input.texcoord;
                float2 local = (uv - _FlowRect.xy) / _FlowRect.zw;
                float window = tex2D(_WindowTex, local).a;
                float4 flowSample = tex2D(_FlowTex, local);
                float2 flow = flowSample.rg * 2.0 - 1.0;
                // Two phases half a period apart, each pulling the picture along the flow for as long as its phase has run
                // and cross-faded by a triangle weight that is zero as a phase starts again, so the loop never jumps. The
                // noise offsets each pixel's phase, so the phases do not fade in unison.
                float phaseA = frac(_FlowPhase + flowSample.b * _NoiseStrength);
                float phaseB = frac(phaseA + 0.5);
                float weightA = 1.0 - abs(1.0 - 2.0 * phaseA);
                half4 pulledA = tex2D(_MainTex, uv - flow * _FlowSpan.xy * phaseA);
                half4 pulledB = tex2D(_MainTex, uv - flow * _FlowSpan.xy * phaseB);
                half4 color = input.color * (lerp(pulledB, pulledA, weightA) + _TextureSampleAdd);
                color.a *= window;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
