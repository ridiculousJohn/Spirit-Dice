Shader "Dice/Rule Die Roots"
{
    // A die-shaped shell textured with roots that wrap the die's four sides and creep over its top, mapped in the die's
    // resting frame: the object axis nearest world up is the die's up, so the roots stay on the die through a roll.
    // Texture u runs around the four sides (seamless when the texture tiles horizontally) and texture v is the growth
    // coordinate: 0 at the base, Side Share at the top edge, 1 at the centre of the top face. Coverage reveals the roots
    // from the base upward with a ragged growth front; the base face is never drawn.
    Properties
    {
        _MainTex ("Roots (RGBA)", 2D) = "white" {}
        _Tint ("Bark Tint", Color) = (1,1,1,1)
        _Ambient ("Ambient Light", Color) = (0.3,0.32,0.36,1)
        _KeyColor ("Key Light", Color) = (0.6,0.62,0.7,1)
        _GlowColor ("Glow Color", Color) = (0.78,0.9,0.32,1)
        _Glow ("Glow", Float) = 0
        _GlowFromTexture ("Glow From Texture Brightness", Range(0, 1)) = 0
        _FrontGlow ("Growth Front Glow", Float) = 0
        _FrontWidth ("Growth Front Width", Range(0.01, 1)) = 0.12
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Coverage ("Coverage", Range(0, 1)) = 1
        _Pulse ("Pulse", Float) = 1
        _Cutoff ("Alpha Cutoff (0 blends softly)", Range(0, 1)) = 0.5
        _GrowthSoftness ("Growth Front Raggedness", Range(0, 0.5)) = 0.12
        _SideShare ("Side Share Of Growth", Range(0.1, 0.95)) = 0.7
        _Repeats ("Texture Repeats Around The Sides", Float) = 2
        _HalfExtent ("Object Half Extent", Float) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Pass
        {
            Name "Rule Die Roots"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _Ambient;
                half4 _KeyColor;
                half4 _GlowColor;
                float _Glow;
                float _GlowFromTexture;
                float _FrontGlow;
                float _FrontWidth;
                float _Opacity;
                float _Coverage;
                float _Pulse;
                float _Cutoff;
                float _GrowthSoftness;
                float _SideShare;
                float _Repeats;
                float _HalfExtent;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float2 samplePosition)
            {
                float3 value = frac(float3(samplePosition.xyx) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float Noise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 blend = frac(samplePosition);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), blend.x), blend.y);
            }

            /** The object axis nearest the given object-space direction, as a signed unit axis. */
            float3 NearestAxis(float3 direction)
            {
                float3 magnitude = abs(direction);
                if (magnitude.x >= magnitude.y && magnitude.x >= magnitude.z) return float3(sign(direction.x), 0, 0);
                if (magnitude.y >= magnitude.z) return float3(0, sign(direction.y), 0);
                return float3(0, 0, sign(direction.z));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 up = NearestAxis(mul((float3x3)GetWorldToObjectMatrix(), float3(0, 1, 0)));
                // The side axes follow the up axis cyclically, so the sides are always visited in the same turning order.
                float3 sideA = abs(up.x) > 0.5 ? float3(0, 1, 0) : abs(up.y) > 0.5 ? float3(0, 0, 1) : float3(1, 0, 0);
                float3 sideB = cross(up, sideA);
                float3 p = input.positionOS / _HalfExtent * 0.5;
                float height = dot(p, up);
                float a = dot(p, sideA);
                float b = dot(p, sideB);
                float across = max(abs(a), abs(b));
                if (height < -across) discard;

                // Side k faces n_k with n_(k+1) = cross(up, n_k); its u runs along n_(k+1), so u meets at every corner.
                float side;
                float along;
                if (abs(a) >= abs(b)) { side = a > 0 ? 0 : 2; along = a > 0 ? b : -b; }
                else { side = b > 0 ? 1 : 3; along = b > 0 ? -a : a; }
                float perimeter = (side + saturate(along + 0.5)) / 4;
                float growth = height >= across
                    ? _SideShare + saturate((0.5 - across) / 0.5) * (1 - _SideShare)
                    : saturate(height + 0.5) * _SideShare;
                float2 uv = float2(perimeter * _Repeats, growth) * _MainTex_ST.xy + _MainTex_ST.zw;
                // The perimeter wraps from 1 to 0 on one edge; removing whole turns keeps the mip level there.
                float2 uvDx = ddx(uv);
                float2 uvDy = ddy(uv);
                uvDx.x -= round(uvDx.x);
                uvDy.x -= round(uvDy.x);
                half4 roots = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, uvDx, uvDy);

                float front = lerp(-_GrowthSoftness, 1 + _GrowthSoftness, saturate(_Coverage));
                float ragged = Noise(float2(perimeter * 48, growth * 9)) - 0.5;
                float reach = front - _GrowthSoftness * ragged - growth;
                if (reach < 0) discard;
                half alpha = roots.a * saturate(_Opacity);
                if (_Cutoff > 0)
                {
                    clip(alpha - _Cutoff);
                    alpha = 1;
                }

                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 lighting = _Ambient.rgb + _KeyColor.rgb * saturate(dot(normal, light.direction));
                half3 color = roots.rgb * _Tint.rgb * lighting;
                half brightness = dot(roots.rgb, half3(0.299, 0.587, 0.114));
                half glow = _Glow * lerp(1, brightness, _GlowFromTexture);
                // The growth front glows as it climbs and fades once the roots have closed.
                half frontGlow = _FrontGlow * saturate(1 - reach / _FrontWidth) * saturate((1 - _Coverage) * 8);
                color += _GlowColor.rgb * (glow + frontGlow) * _Pulse;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
