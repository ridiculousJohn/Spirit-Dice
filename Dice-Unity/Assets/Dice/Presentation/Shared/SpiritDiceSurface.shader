Shader "Dice/Spirit Dice Surface"
{
    Properties
    {
        _BaseColor ("Surface Color", Color) = (1,1,1,1)
        _SurfaceTexture ("Surface Texture", 2D) = "white" {}
        _ShowPips ("Show Pips", Float) = 1
        _FaceIcon ("Top Face Icon", 2D) = "white" {}
        _FaceIconRect ("Top Face Icon UV Rect", Vector) = (0,0,1,1)
        _FaceIconUVTransform ("Top Face Icon UV Transform", Vector) = (1,1,0,0)
        _FaceIconColor ("Top Face Ink", Color) = (0.04,0.03,0.02,1)
        _ShowFaceIcon ("Show Top Face Icon", Float) = 0
        _ClipWorldX ("World X Clip Bounds", Vector) = (0,0,0,0)
        _SurfaceKind ("Surface Kind", Float) = 0
        _Seed ("Surface Seed", Float) = 0
        _GrainScale ("Grain Scale", Vector) = (1,1,1,0)
        _ShadowLift ("Shadow Lift", Float) = 0
        _Smoothness ("Smoothness", Float) = 0.23
        [HideInInspector] _SceneLightingEnabled ("Scene Lighting Enabled", Float) = 0
        [HideInInspector] _SceneLightingAmbient ("Scene Lighting Ambient", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightingKeyColor ("Scene Lighting Key Color", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightingKeyDirection ("Scene Lighting Key Direction", Vector) = (0,1,0,0)
        [HideInInspector] _SceneLightPosition0 ("Scene Light Position 0", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightPosition1 ("Scene Light Position 1", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightPosition2 ("Scene Light Position 2", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightPosition3 ("Scene Light Position 3", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightColor0 ("Scene Light Color 0", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightColor1 ("Scene Light Color 1", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightColor2 ("Scene Light Color 2", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightColor3 ("Scene Light Color 3", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightParameters0 ("Scene Light Parameters 0", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightParameters1 ("Scene Light Parameters 1", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightParameters2 ("Scene Light Parameters 2", Vector) = (0,0,0,0)
        [HideInInspector] _SceneLightParameters3 ("Scene Light Parameters 3", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Spirit Dice Surface"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _SurfaceTexture_ST;
                float _ShowPips;
                float4 _FaceIconRect;
                float4 _FaceIconUVTransform;
                half4 _FaceIconColor;
                float _ShowFaceIcon;
                float4 _ClipWorldX;
                float _SurfaceKind;
                float _Seed;
                float4 _GrainScale;
                float _ShadowLift;
                float _Smoothness;
                float _SceneLightingEnabled;
                float4 _SceneLightingAmbient;
                float4 _SceneLightingKeyColor;
                float4 _SceneLightingKeyDirection;
                float4 _SceneLightPosition0;
                float4 _SceneLightPosition1;
                float4 _SceneLightPosition2;
                float4 _SceneLightPosition3;
                float4 _SceneLightColor0;
                float4 _SceneLightColor1;
                float4 _SceneLightColor2;
                float4 _SceneLightColor3;
                float4 _SceneLightParameters0;
                float4 _SceneLightParameters1;
                float4 _SceneLightParameters2;
                float4 _SceneLightParameters3;
            CBUFFER_END
            TEXTURE2D(_SurfaceTexture);
            SAMPLER(sampler_SurfaceTexture);
            TEXTURE2D(_FaceIcon);
            SAMPLER(sampler_FaceIcon);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;
                float2 surfaceUV : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float face : TEXCOORD4;
                float2 surfaceUV : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                output.face = input.face.x;
                output.surfaceUV = input.surfaceUV;
                return output;
            }

            float Hash(float3 samplePosition)
            {
                samplePosition = frac(samplePosition * 0.1031);
                samplePosition += dot(samplePosition, samplePosition.yzx + 33.33);
                return frac((samplePosition.x + samplePosition.y) * samplePosition.z);
            }

            float Noise(float3 samplePosition)
            {
                float3 cell = floor(samplePosition);
                float3 blend = frac(samplePosition);
                blend = blend * blend * (3 - 2 * blend);
                float lower = lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), blend.x),
                    lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), blend.x), blend.y);
                float upper = lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), blend.x),
                    lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), blend.x), blend.y);
                return lerp(lower, upper, blend.z);
            }

            float PipDistance(float2 samplePosition, float face)
            {
                const float offset = 0.215;
                float distance = 1;
                if (face == 1 || face == 3 || face == 5) distance = length(samplePosition);
                if (face >= 2)
                {
                    distance = min(distance, length(samplePosition - float2(-offset, offset)));
                    distance = min(distance, length(samplePosition - float2(offset, -offset)));
                }
                if (face >= 4)
                {
                    distance = min(distance, length(samplePosition - float2(offset, offset)));
                    distance = min(distance, length(samplePosition - float2(-offset, -offset)));
                }
                if (face == 6)
                {
                    distance = min(distance, length(samplePosition - float2(-offset, 0)));
                    distance = min(distance, length(samplePosition - float2(offset, 0)));
                }
                return distance;
            }

            void AccumulateSceneLight(float3 positionWS, half3 normal, half3 view, half roughness,
                float4 lightPosition, float4 lightColor, float4 parameters,
                inout half3 lighting, inout half3 specularLighting)
            {
                // The sampled level drives both physical light power and low-power artistic falloff.
                float intensity = parameters.x;
                float range = lightPosition.w;
                if (intensity <= 0 || parameters.y <= 0 || range <= 0) return;
                float3 offset = lightPosition.xyz - positionWS;
                float distanceSquared = dot(offset, offset);
                float distance = sqrt(distanceSquared);
                float normalizedDistance = distance / range;
                float attenuation = saturate(1 - normalizedDistance * normalizedDistance);
                attenuation *= attenuation / max(1, distanceSquared);
                attenuation *= intensity * parameters.y
                    * pow(min(intensity, 1), normalizedDistance * parameters.z);
                float3 direction = offset * rsqrt(max(distanceSquared, 0.000001));
                half3 radiance = lightColor.rgb * attenuation;
                half diffuse = saturate(dot(normal, direction));
                half3 halfDirection = SafeNormalize(direction + view);
                half specular = pow(saturate(dot(normal, halfDirection)), lerp(84, 10, roughness));
                lighting += radiance * diffuse * 0.92;
                specularLighting += radiance * specular;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_ClipWorldX.z > 0.5)
                    clip(min(input.positionWS.x - _ClipWorldX.x, _ClipWorldX.y - input.positionWS.x));
                if (_SurfaceKind > 4.5 && _SurfaceKind < 5.5)
                {
                    float2 footprint = input.uv * 2;
                    float radial = saturate(1 - dot(footprint, footprint));
                    half alpha = _BaseColor.a * radial * radial * (1 - _ShadowLift * 0.62);
                    return half4(_BaseColor.rgb, alpha);
                }
                if (_SurfaceKind > 5.5) return half4(_BaseColor.rgb * 1.4, _BaseColor.a);

                float3 samplePosition = input.positionOS + _Seed * float3(0.71, 0.37, 0.53);
                half3 albedo = _BaseColor.rgb;
                half roughness = 0.72;
                half metallic = 0;
                half cavity = 1;
                if (_SurfaceKind < 0.5)
                {
                    float fine = Noise(samplePosition * 76);
                    float2 boneUV = input.uv + 0.5 + frac(float2(_Seed * 0.13, input.face * 0.173));
                    albedo *= SAMPLE_TEXTURE2D(_SurfaceTexture, sampler_SurfaceTexture, boneUV).rgb;
                    float distance = PipDistance(input.uv, floor(input.face + 0.5));
                    float pigment = (1 - smoothstep(0.061, 0.084, distance)) * _ShowPips;
                    albedo = lerp(albedo, half3(0.022, 0.017, 0.011) * (0.8 + fine * 0.4), pigment);
                    cavity = lerp(1, 0.68, pigment);
                    roughness = lerp(0.38, 0.63, pigment);
                    if (_ShowFaceIcon > 0.5 && input.face > 1.5 && input.face < 2.5)
                    {
                        float2 iconUV = input.uv / 0.68 + 0.5;
                        float inside = step(0, iconUV.x) * step(iconUV.x, 1) * step(0, iconUV.y) * step(iconUV.y, 1);
                        iconUV = (saturate(iconUV) - 0.5) * _FaceIconUVTransform.xy + 0.5;
                        half4 icon = SAMPLE_TEXTURE2D(_FaceIcon, sampler_FaceIcon,
                            _FaceIconRect.xy + iconUV * _FaceIconRect.zw);
                        half ink = icon.a * inside;
                        albedo = lerp(albedo, _FaceIconColor.rgb * (0.84 + fine * 0.24), ink);
                        cavity = lerp(cavity, 0.72, ink);
                        roughness = lerp(roughness, 0.67, ink);
                    }
                }
                else if (_SurfaceKind < 1.5)
                {
                    float2 woodUV = input.surfaceUV * _SurfaceTexture_ST.xy + _SurfaceTexture_ST.zw;
                    albedo *= SAMPLE_TEXTURE2D(_SurfaceTexture, sampler_SurfaceTexture, woodUV).rgb;
                    float edge = smoothstep(0.72, 0.98, max(abs(input.uv.x), abs(input.uv.y)) * 2);
                    float wear = smoothstep(0.48, 0.78, Noise(samplePosition * 39)) * edge;
                    albedo = lerp(albedo, half3(0.39, 0.19, 0.058), wear * 0.58);
                    roughness = 0.77;
                }
                else if (_SurfaceKind < 2.5)
                {
                    float fiber = Hash(floor(input.positionOS * _GrainScale.xyz * 125 + _Seed));
                    float worn = Noise(input.positionOS * _GrainScale.xyz * 3 + _Seed);
                    albedo *= 0.68 + fiber * 0.38 + worn * 0.17;
                    float wallDistance = min(0.5 - abs(input.uv.x), 0.5 - abs(input.uv.y));
                    cavity *= lerp(0.3, 1, smoothstep(0, 0.16, wallDistance));
                    roughness = 0.98;
                }
                else if (_SurfaceKind < 3.5)
                {
                    float tarnish = Noise(samplePosition * 14);
                    float scratches = Hash(floor(samplePosition * 130));
                    albedo = lerp(_BaseColor.rgb * 0.36, _BaseColor.rgb * 1.14, smoothstep(0.2, 0.73, tarnish));
                    albedo *= 0.89 + scratches * 0.17;
                    roughness = 0.37;
                    metallic = 0.62;
                }

                half3 normal = normalize(input.normalWS);
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 specularColor = lerp(half3(0.18, 0.14, 0.085), albedo * 0.58, metallic);
                if (_SceneLightingEnabled > 0.5)
                {
                    half3 lighting = _SceneLightingAmbient.rgb;
                    half3 direction = _SceneLightingKeyDirection.xyz;
                    half3 key = _SceneLightingKeyColor.rgb;
                    lighting += key * saturate(dot(normal, direction)) * 0.92;
                    half3 halfDirection = SafeNormalize(direction + view);
                    half specular = pow(saturate(dot(normal, halfDirection)), lerp(84, 10, roughness));
                    half3 specularLighting = key * specular;
                    AccumulateSceneLight(input.positionWS, normal, view, roughness,
                        _SceneLightPosition0, _SceneLightColor0, _SceneLightParameters0, lighting, specularLighting);
                    AccumulateSceneLight(input.positionWS, normal, view, roughness,
                        _SceneLightPosition1, _SceneLightColor1, _SceneLightParameters1, lighting, specularLighting);
                    AccumulateSceneLight(input.positionWS, normal, view, roughness,
                        _SceneLightPosition2, _SceneLightColor2, _SceneLightParameters2, lighting, specularLighting);
                    AccumulateSceneLight(input.positionWS, normal, view, roughness,
                        _SceneLightPosition3, _SceneLightColor3, _SceneLightParameters3, lighting, specularLighting);
                    return half4(albedo * lighting * cavity + specularColor * specularLighting, _BaseColor.a);
                }
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normal, light.direction));
                half3 ambient = half3(0.095, 0.10, 0.12);
                half3 lighting = ambient + light.color * diffuse * light.shadowAttenuation * 0.92;
                half3 halfDirection = normalize(light.direction + view);
                half specular = pow(saturate(dot(normal, halfDirection)), lerp(84, 10, roughness));
                half3 color = albedo * lighting * cavity + specularColor * light.color * specular * light.shadowAttenuation;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float4 ShadowVertex(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif
                return positionCS;
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
