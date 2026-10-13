Shader "Dice/Spirit Panel Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frame Texture", 2D) = "white" {}
        _SurfaceTex ("Panel Surface", 2D) = "white" {}
        _SurfaceColor ("Surface Tint", Color) = (1,1,1,1)
        _SurfaceTileSize ("Surface Tile Size", Float) = 256
        _TrimChromaStart ("Trim Chroma Start", Float) = 0.008
        _TrimChromaEnd ("Trim Chroma End", Float) = 0.04
        _TrimInterior ("Trim-free Interior UV Bounds", Vector) = (0,0,0,0)
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Spirit Panel Surface UI"
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
            sampler2D _MainTex;
            sampler2D _SurfaceTex;
            float _SurfaceTileSize;
            float _TrimChromaStart;
            float _TrimChromaEnd;
            float4 _TrimInterior;
            fixed4 _Color;
            fixed4 _SurfaceColor;
            fixed4 _TextureSampleAdd;
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

            float4 frag(v2f input) : SV_Target
            {
                float4 frame = tex2D(_MainTex, input.texcoord) + _TextureSampleAdd;
                float3 surface = tex2D(_SurfaceTex, input.localPosition.xy / max(_SurfaceTileSize, 0.001)).rgb;
                float3 surfaceTint = _SurfaceColor.rgb;
                #ifdef UNITY_COLORSPACE_GAMMA
                frame.rgb = GammaToLinearSpace(frame.rgb);
                surface = GammaToLinearSpace(surface);
                surfaceTint = GammaToLinearSpace(surfaceTint);
                #endif
                surface *= surfaceTint;
                // Warm trim retains the authored frame; green pixels share a surface independent of the sliced UVs.
                float trim = smoothstep(_TrimChromaStart, max(_TrimChromaEnd, _TrimChromaStart + 0.0001), frame.r - frame.g);
                float2 inside = step(_TrimInterior.xy, input.texcoord) * step(input.texcoord, _TrimInterior.zw);
                trim *= 1 - inside.x * inside.y;
                float3 rgb = lerp(surface, frame.rgb, trim);
                #ifdef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif
                float4 color = float4(rgb, frame.a) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
