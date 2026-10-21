#ifndef DICE_SCENE_LIT_IMAGE_INCLUDED
// Shared by the Dice/Scene Lit Image UI shader and its Hidden/Dice/Scene Lit Image Preview blit shader.
#define DICE_SCENE_LIT_IMAGE_INCLUDED

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
sampler2D _SourceTex, _LayerMask, _SceneLitDepthTex;
sampler2D _SceneLitReferenceTex0, _SceneLitReferenceTex1, _SceneLitReferenceTex2, _SceneLitReferenceTex3;
sampler2D _SceneLitResponseTex0, _SceneLitResponseTex1, _SceneLitResponseTex2, _SceneLitResponseTex3;
float _SceneLitMode0, _SceneLitMode1, _SceneLitMode2, _SceneLitMode3;
float _SceneLitResponseEnabled0, _SceneLitResponseEnabled1, _SceneLitResponseEnabled2, _SceneLitResponseEnabled3;
float _SceneLitStrength0, _SceneLitStrength1, _SceneLitStrength2, _SceneLitStrength3;
float _SceneLitIntensity0, _SceneLitIntensity1, _SceneLitIntensity2, _SceneLitIntensity3;
float4 _SceneLitTint0, _SceneLitTint1, _SceneLitTint2, _SceneLitTint3;
float _SceneLitDistanceFalloff0, _SceneLitDistanceFalloff1, _SceneLitDistanceFalloff2, _SceneLitDistanceFalloff3;
float _SceneLitSourceResponse0, _SceneLitSourceResponse1, _SceneLitSourceResponse2, _SceneLitSourceResponse3;
float _SceneLitIncludesBase0, _SceneLitIncludesBase1, _SceneLitIncludesBase2, _SceneLitIncludesBase3;
float _SceneLitConstrainAmbient0, _SceneLitConstrainAmbient1, _SceneLitConstrainAmbient2, _SceneLitConstrainAmbient3;
float _SceneLitModulateByBase0, _SceneLitModulateByBase1, _SceneLitModulateByBase2, _SceneLitModulateByBase3;
float4 _SceneLitPosition0, _SceneLitPosition1, _SceneLitPosition2, _SceneLitPosition3;
float4 _SceneLitColor0, _SceneLitColor1, _SceneLitColor2, _SceneLitColor3;
float4 _SceneLitDepthRange, _SceneLitFrameOrigin, _SceneLitFrameAxisX, _SceneLitFrameAxisY, _SceneLitFrameTransform, _SceneLitMapRect;
float _SceneLitWrap, _InvertMask;
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
    output.texcoord = input.texcoord;
    if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
        input.color.rgb = UIGammaToLinear(input.color.rgb);
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

float Mode(float value, float mode)
{
    return abs(value - mode) < 0.5 ? 1.0 : 0.0;
}

// Matches Scene Lighting, so references dim and flicker exactly as the background does.
float ResponseScale(float intensity, float2 response, float responseEnabled, float distanceFalloff, float sourceResponse)
{
    if (intensity <= 0.0) return 0.0;
    if (responseEnabled < 0.5) return intensity;
    float surfaceScale = intensity * pow(min(intensity, 1.0), saturate(response.r) * distanceFalloff);
    float sourceScale = pow(intensity, sourceResponse);
    return lerp(surfaceScale, sourceScale, saturate(response.g));
}

float3 Contribution(float3 sampled, float3 ambient, float includesBase, float3 tint, float intensity, float modulate)
{
    return max(sampled - ambient * includesBase, 0.0) * tint * intensity * lerp(float3(1,1,1), ambient, modulate);
}

// Matches the flat receiver falloff; wrap blends a viewer-facing surface toward direction-free light.
float3 PointLight(float3 position, float4 light, float4 color, float intensity, float distanceFalloff)
{
    if (intensity <= 0.0 || color.a <= 0.0) return float3(0, 0, 0);
    float3 offset = light.xyz - position;
    float distanceSquared = dot(offset, offset);
    float lightDistance = sqrt(distanceSquared);
    float normalizedDistance = saturate(lightDistance / max(light.w, 0.01));
    float edge = saturate(1.0 - normalizedDistance * normalizedDistance);
    float attenuation = edge * edge / max(1.0, distanceSquared);
    float response = intensity * pow(min(intensity, 1.0), normalizedDistance * distanceFalloff);
    float facing = lightDistance > 0.0001 ? saturate(-offset.z / lightDistance) : 1.0;
    float diffuse = lerp(facing, 1.0, _SceneLitWrap);
    return color.rgb * (color.a * response * attenuation * diffuse);
}

float4 Compose(float2 uv)
{
    float4 source = tex2D(_SourceTex, uv) + _TextureSampleAdd;
    float3 ambient = DecodeColor(source.rgb);
    float2 frame = uv * _SceneLitFrameTransform.xy + _SceneLitFrameTransform.zw;
    // A cropped image covers only part of a full-frame response or depth map.
    float2 mapUv = _SceneLitMapRect.xy + frame * _SceneLitMapRect.zw;
    float reference0 = Mode(_SceneLitMode0, 1.0);
    float reference1 = Mode(_SceneLitMode1, 1.0);
    float reference2 = Mode(_SceneLitMode2, 1.0);
    float reference3 = Mode(_SceneLitMode3, 1.0);
    float computed0 = Mode(_SceneLitMode0, 2.0);
    float computed1 = Mode(_SceneLitMode1, 2.0);
    float computed2 = Mode(_SceneLitMode2, 2.0);
    float computed3 = Mode(_SceneLitMode3, 2.0);
    float3 sampled0 = DecodeColor(tex2D(_SceneLitReferenceTex0, uv).rgb);
    float3 sampled1 = DecodeColor(tex2D(_SceneLitReferenceTex1, uv).rgb);
    float3 sampled2 = DecodeColor(tex2D(_SceneLitReferenceTex2, uv).rgb);
    float3 sampled3 = DecodeColor(tex2D(_SceneLitReferenceTex3, uv).rgb);
    // All references subtract the same ambient, constrained before any contribution is separated.
    if (reference0 * _SceneLitIncludesBase0 * _SceneLitConstrainAmbient0 > 0.5) ambient = min(ambient, sampled0);
    if (reference1 * _SceneLitIncludesBase1 * _SceneLitConstrainAmbient1 > 0.5) ambient = min(ambient, sampled1);
    if (reference2 * _SceneLitIncludesBase2 * _SceneLitConstrainAmbient2 > 0.5) ambient = min(ambient, sampled2);
    if (reference3 * _SceneLitIncludesBase3 * _SceneLitConstrainAmbient3 > 0.5) ambient = min(ambient, sampled3);
    float scale0 = reference0 * ResponseScale(_SceneLitIntensity0, tex2D(_SceneLitResponseTex0, mapUv).rg, _SceneLitResponseEnabled0, _SceneLitDistanceFalloff0, _SceneLitSourceResponse0);
    float scale1 = reference1 * ResponseScale(_SceneLitIntensity1, tex2D(_SceneLitResponseTex1, mapUv).rg, _SceneLitResponseEnabled1, _SceneLitDistanceFalloff1, _SceneLitSourceResponse1);
    float scale2 = reference2 * ResponseScale(_SceneLitIntensity2, tex2D(_SceneLitResponseTex2, mapUv).rg, _SceneLitResponseEnabled2, _SceneLitDistanceFalloff2, _SceneLitSourceResponse2);
    float scale3 = reference3 * ResponseScale(_SceneLitIntensity3, tex2D(_SceneLitResponseTex3, mapUv).rg, _SceneLitResponseEnabled3, _SceneLitDistanceFalloff3, _SceneLitSourceResponse3);
    float3 light = Contribution(sampled0, ambient, _SceneLitIncludesBase0, _SceneLitTint0.rgb, scale0, _SceneLitModulateByBase0);
    light += Contribution(sampled1, ambient, _SceneLitIncludesBase1, _SceneLitTint1.rgb, scale1, _SceneLitModulateByBase1);
    light += Contribution(sampled2, ambient, _SceneLitIncludesBase2, _SceneLitTint2.rgb, scale2, _SceneLitModulateByBase2);
    light += Contribution(sampled3, ambient, _SceneLitIncludesBase3, _SceneLitTint3.rgb, scale3, _SceneLitModulateByBase3);
    // Computed lights reach the pixel at its frame position, moved toward the viewer by its depth.
    float3 position = _SceneLitFrameOrigin.xyz + frame.x * _SceneLitFrameAxisX.xyz + frame.y * _SceneLitFrameAxisY.xyz;
    position.z += lerp(_SceneLitDepthRange.x, _SceneLitDepthRange.y, tex2D(_SceneLitDepthTex, mapUv).r);
    float3 computedLight = computed0 * _SceneLitStrength0 * PointLight(position, _SceneLitPosition0, _SceneLitColor0, _SceneLitIntensity0, _SceneLitDistanceFalloff0);
    computedLight += computed1 * _SceneLitStrength1 * PointLight(position, _SceneLitPosition1, _SceneLitColor1, _SceneLitIntensity1, _SceneLitDistanceFalloff1);
    computedLight += computed2 * _SceneLitStrength2 * PointLight(position, _SceneLitPosition2, _SceneLitColor2, _SceneLitIntensity2, _SceneLitDistanceFalloff2);
    computedLight += computed3 * _SceneLitStrength3 * PointLight(position, _SceneLitPosition3, _SceneLitColor3, _SceneLitIntensity3, _SceneLitDistanceFalloff3);
    float3 color = ambient + light + computedLight * ambient;
    #ifdef UNITY_COLORSPACE_GAMMA
    color = LinearToGammaSpace(color);
    #endif
    return float4(color, source.a);
}

float4 frag(v2f input) : SV_Target
{
    // Match native UI alpha precision before applying the ownership mask.
    const half alphaPrecision = half(0xff);
    const half invAlphaPrecision = half(1.0 / alphaPrecision);
    input.color.a = round(input.color.a * alphaPrecision) * invAlphaPrecision;
    float selected = tex2D(_LayerMask, input.texcoord).a;
    float coverage = lerp(selected, 1.0 - selected, _InvertMask);
    half4 color = input.color * Compose(input.texcoord);
    color.a *= coverage;
    #ifdef UNITY_UI_CLIP_RECT
    color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
    #endif
    color.rgb *= color.a;
    return color;
}

float4 fragPreview(v2f input) : SV_Target
{
    return Compose(input.texcoord);
}

#endif
