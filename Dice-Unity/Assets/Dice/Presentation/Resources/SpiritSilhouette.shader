Shader "Dice/Spirit Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
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
            "CanUseSpriteAtlas" = "True"
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
            Name "Spirit Silhouette"
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.texcoord);
                float2 radius = max(_MainTex_TexelSize.xy * 10.0, float2(0.01, 0.01));
                // Neighbor alpha feathers the fur outline without flattening the face detail.
                half softAlpha = source.a * 0.20;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(radius.x, 0)).a * 0.10;
                softAlpha += tex2D(_MainTex, input.texcoord - float2(radius.x, 0)).a * 0.10;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(0, radius.y)).a * 0.10;
                softAlpha += tex2D(_MainTex, input.texcoord - float2(0, radius.y)).a * 0.10;
                softAlpha += tex2D(_MainTex, input.texcoord + radius).a * 0.06;
                softAlpha += tex2D(_MainTex, input.texcoord - radius).a * 0.06;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(radius.x, -radius.y)).a * 0.06;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(-radius.x, radius.y)).a * 0.06;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(radius.x * 2, 0)).a * 0.04;
                softAlpha += tex2D(_MainTex, input.texcoord - float2(radius.x * 2, 0)).a * 0.04;
                softAlpha += tex2D(_MainTex, input.texcoord + float2(0, radius.y * 2)).a * 0.04;
                softAlpha += tex2D(_MainTex, input.texcoord - float2(0, radius.y * 2)).a * 0.04;
                half luminance = dot(source.rgb, half3(0.2126, 0.7152, 0.0722));
                half shimmer = 0.94 + 0.06 * sin(input.texcoord.y * 44 + _Time.y * 1.4
                    + sin(input.texcoord.x * 25 - _Time.y * 0.7));
                fixed4 color = fixed4(input.color.rgb * (0.42 + luminance * 0.78),
                    softAlpha * input.color.a * (0.65 + luminance * 0.35) * shimmer);
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
