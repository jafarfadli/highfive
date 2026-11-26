// Cel lighting for the inked look of Be Korean!, shared by the Surface, Character and Hair shaders.
// Surfaces keep their own colours: the light is cut into three tones (shadow, half, lit) with narrow
// anti-aliased steps instead of a smooth falloff, the terminator band and cast shadows are printed with
// halftone dots, and highlights are crisp spots. Ambient light, sky reflections and SSAO work as in URP
// Lit; callers apply fog. Include after Lighting.hlsl.
#ifndef SEOUL_INK_LIGHTING_INCLUDED
#define SEOUL_INK_LIGHTING_INCLUDED

// 1 in a headset (set by VRMode): patterns fixed to the screen would sit on the lens there, so the
// halftone and the hatching are left out and the cel tones and ink lines carry the look
float _SFW_VR;

// Light amount (N·L × shadow, 0..1) → the direct light a cel-shaded surface receives: none in shadow,
// 0.36 in the half tone, 0.8 when lit. The levels are the averages of the smooth N·L they replace, so a
// scene keeps its overall brightness and colours; only the gradient turns into flat steps.
static const half CelLit = 0.8h;

half CelTone(half x)
{
    const half s = 0.035h;
    half half1 = smoothstep(0.06h - s, 0.06h + s, x);
    half lit = smoothstep(0.42h - s, 0.42h + s, x);
    return 0.36h * half1 + 0.44h * lit;
}

// Halftone dots on a 45° screen; density 0 = none, 1 = full coverage. Returns 1 inside a dot.
half Halftone(float2 positionCS, half density)
{
    float scale = max(_ScreenParams.y / 1080.0, 0.5);
    float2 p = positionCS / (7.0 * scale);
    float2 r = float2(p.x + p.y, p.x - p.y) * 0.70710678;
    float2 f = frac(r) - 0.5;
    float d = length(f);
    float rad = sqrt(saturate(density)) * 0.62;
    float aa = max(fwidth(d), 1e-4) * 0.9;
    return 1.0h - smoothstep(rad - aa, rad + aa, d);
}

// Ambient light in three flat tones by facing (up, sides, down) instead of a smooth gradient, so even
// under soft indoor light every box reads like a drawn one: lit top, mid sides, dark underside. The
// tones are the ambient the surface would get facing that way, so colours stay the same.
half3 CelAmbient(half3 n)
{
    half3 flat = half3(n.x, 0, n.z);
    flat = dot(flat, flat) > 1e-4h ? normalize(flat) : half3(1, 0, 0);
    half3 side = SampleSH(flat);
    half3 up = SampleSH(half3(0, 1, 0));
    half3 down = SampleSH(half3(0, -1, 0));
    const half s = 0.04h;
    half tUp = smoothstep(0.55h - s, 0.55h + s, n.y);
    half tDown = smoothstep(-0.55h + s, -0.55h - s, n.y);
    return lerp(lerp(side, up, tUp), down, tDown);
}

// Diagonal hatching in screen space: 45° lines 5 px apart at 1080p (measured across them), 0.9–1.6 px
// wide as the shade deepens, with a one-pixel anti-aliased edge. Returns the line coverage × amount.
half Hatching(float2 positionCS, half amount)
{
    if (amount <= 0.001h) return 0;
    float scale = max(_ScreenParams.y / 1080.0, 0.5);
    float period = 5.0 * scale;
    float across = (positionCS.x - positionCS.y) * 0.70710678;
    float t = abs(frac(across / period) - 0.5) * period;   // px from the nearest line's centre
    float halfWidth = (0.45 + 0.35 * amount) * scale;
    return (1.0 - smoothstep(halfWidth - 0.5, halfWidth + 0.5, t)) * amount;
}

struct InkSurface
{
    half3 albedo;
    half smoothness;
    half metallic;
    half occlusion;
    half halftone;   // how strongly shade is printed with dots (0 = never)
};

// One light: banded diffuse plus a crisp specular spot. Returns the light's contribution; adds the
// light's shade (1 − tone) to `shade` for the halftone.
half3 InkLight(Light light, InkSurface s, BRDFData brdf, half3 normalWS, half3 viewDirWS, bool mainLight, inout half shade)
{
    half nl = dot(normalWS, light.direction);
    half amount = saturate(nl) * light.shadowAttenuation;
    half tone = CelTone(amount);
    half3 radiance = light.color * light.distanceAttenuation;
    half3 diffuse = brdf.diffuse * radiance * tone;

    half3 h = SafeNormalize(light.direction + viewDirWS);
    half nh = saturate(dot(normalWS, h));
    half power = exp2(10.0h * s.smoothness + 1.0h);
    half spot = smoothstep(0.5h, 0.56h, pow(nh, power)) * step(0.0h, nl) * light.shadowAttenuation;
    half3 specular = brdf.specular * radiance * spot * (s.smoothness * 2.2h);

    if (mainLight) shade = max(shade, 1.0h - tone / CelLit);
    return diffuse + specular;
}

half4 InkFragment(InputData inputData, InkSurface s)
{
    SurfaceData sd = (SurfaceData)0;
    sd.albedo = s.albedo;
    sd.metallic = s.metallic;
    sd.smoothness = s.smoothness;
    sd.occlusion = s.occlusion;
    sd.alpha = 1;
    BRDFData brdf;
    half alpha = 1;
    InitializeBRDFData(s.albedo, s.metallic, half3(0, 0, 0), s.smoothness, alpha, brdf);

    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData, sd);
    half3 n = inputData.normalWS;
    half3 v = inputData.viewDirectionWS;

    // ambient and sky reflection, as in URP Lit
    half3 reflectVector = reflect(-v, n);
    half fresnel = Pow4(1.0h - saturate(dot(n, v)));
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, brdf.perceptualRoughness,
        ao.indirectAmbientOcclusion, inputData.normalizedScreenSpaceUV);
    half3 ambient = CelAmbient(n);
    half3 color = EnvironmentBRDF(brdf, ambient * ao.indirectAmbientOcclusion, indirectSpecular, fresnel);

    half shade = 0;
    half4 shadowMask = inputData.shadowMask;
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    mainLight.color *= ao.directAmbientOcclusion;
    color += InkLight(mainLight, s, brdf, n, v, true, shade);

    #if defined(_ADDITIONAL_LIGHTS)
        uint pixelLightCount = GetAdditionalLightsCount();
        #if USE_CLUSTER_LIGHT_LOOP
        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
            light.color *= ao.directAmbientOcclusion;
            color += InkLight(light, s, brdf, n, v, false, shade);
        }
        #endif
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
            light.color *= ao.directAmbientOcclusion;
            color += InkLight(light, s, brdf, n, v, false, shade);
        LIGHT_LOOP_END
    #endif

    // shade printed as dots: where the sun doesn't reach (counted only as far as the sun outshines the
    // ambient, so a weak indoor sun doesn't dot every wall) and on faces turned away from the ambient light;
    // only near the camera, where the dots read as print rather than noise
    if (s.halftone * (1.0h - _SFW_VR) > 0.001h)
    {
        half sunLum = dot(mainLight.color, half3(0.299h, 0.587h, 0.114h));
        half upLum = dot(SampleSH(half3(0, 1, 0)), half3(0.299h, 0.587h, 0.114h)) + 1e-3h;
        half here = dot(ambient, half3(0.299h, 0.587h, 0.114h));
        shade = max(shade * saturate(sunLum / (upLum * 2.0h)), saturate(1.0h - here / upLum) * 1.4h);
        float dist = distance(inputData.positionWS, GetCameraPositionWS());
        half near = 1.0h - smoothstep(25.0h, 60.0h, dist);
        half dots = Halftone(inputData.positionCS.xy, shade * 0.55h) * near * s.halftone;
        // deep shade up close is hatched as well, like the shadows of an inked page
        half hatch = Hatching(inputData.positionCS.xy, smoothstep(0.55h, 0.78h, shade)) * (1.0h - smoothstep(6.0h, 18.0h, dist)) * s.halftone;
        color *= (1.0h - 0.16h * dots) * (1.0h - 0.26h * hatch);
    }
    return half4(color, 1); // the caller applies fog after its own extras
}

#endif
