// Hair, inked: a flat two-tone mass. Strand textures tinted by the vertex colour (hair colour, alpha =
// ambient occlusion, taken as one step) give thin dark strand lines; light is cel-toned, ambient comes in
// three flat tones, and one smooth-edged Kajiya-Kay shine band runs across each lock, cut only by the strand
// lines. The strand direction is the direction in which the texture's v grows (from the crown down), found
// from screen derivatives, so the mesh needs no tangents.
// TEXCOORD0: u around the head, v down the strands.   TEXCOORD1.x: strand texture slice.
// Reuses the URP Lit shadow/depth passes.
Shader "SeoulFirstWeek/Hair"
{
    Properties
    {
        [NoScaleOffset] _HairArray ("Strands (R = strand shade)", 2DArray) = "" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Highlight strength", Range(0, 2)) = 1

        [HideInInspector] _BaseMap ("Albedo", 2D) = "white" {}
        [HideInInspector] _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "InkLighting.hlsl"

            TEXTURE2D_ARRAY(_HairArray); SAMPLER(sampler_HairArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 mat : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float2 uv : TEXCOORD3;
                nointerpolation float slice : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = v.uv;
                o.slice = v.mat.x;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Strand(half3 t, half3 h, half exponent)
            {
                half th = dot(t, h);
                half sinTH = sqrt(max(0.0h, 1.0h - th * th));
                return smoothstep(-1.0h, 0.0h, th) * pow(sinTH, exponent);
            }

            half3 HairLight(Light light, half3 n, half3 t, half3 v, half3 albedo, half strand, half specScale)
            {
                half3 l = light.direction;
                half nl = dot(n, l);
                half3 radiance = light.color * light.distanceAttenuation;
                // cel tones like every other surface, with a little wrap round the terminator
                half diffuse = CelTone(saturate(nl + 0.12h) * light.shadowAttenuation) / CelLit * 0.92h;
                half3 h = normalize(l + v);
                half3 t1 = normalize(t + n * 0.12h);
                // one flat band of shine across each lock (anime-style): no noise in its position, so its edges
                // stay smooth; cut only by the inked strand lines, and kept modest so it never reads as a pale patch
                half band = smoothstep(0.5h, 0.56h, Strand(t1, h, 48.0h)) * strand;
                half facing = saturate(nl * 2.0h + 0.3h) * light.shadowAttenuation;
                return radiance * (albedo * diffuse + band * (0.09h + albedo * 0.8h) * facing * specScale);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D_ARRAY(_HairArray, sampler_HairArray, i.uv, (uint)round(i.slice));
                half3 n = normalize(i.normalWS);
                // two flat tones: the hair mass, and thin darker strand lines inked where the texture dips
                half strand = smoothstep(0.26h, 0.3h, tex.r);
                half3 albedo = i.color.rgb * _BaseColor.rgb * lerp(0.58h, 1.04h, strand);
                // the painted occlusion (under the fringe, at the nape) as one flat step, not a gradient
                half ao = lerp(0.62h, 1.0h, smoothstep(0.45h, 0.55h, i.color.a));
                albedo *= ao;

                // strand direction: where v grows, flattened onto the surface
                float3 dpdx = ddx(i.positionWS), dpdy = ddy(i.positionWS);
                float2 duvx = ddx(i.uv), duvy = ddy(i.uv);
                float det = duvx.x * duvy.y - duvx.y * duvy.x;
                float3 dpdv = (duvx.x * dpdy - duvy.x * dpdx) / (abs(det) > 1e-12 ? det : 1e-12);
                float3 t = dpdv - n * dot(dpdv, n);
                t = dot(t, t) > 1e-12 ? normalize(t) : normalize(cross(n, float3(1, 0, 0)) + 1e-4);

                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                half4 shadowMask = half4(1, 1, 1, 1);

                half ssao = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    ssao = aoFactor.indirectAmbientOcclusion;
                #endif

                half3 color = CelAmbient(n) * albedo * ssao;
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half specScale = _Smoothness * ao * ao;
                color += HairLight(mainLight, n, t, v, albedo, strand, specScale);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                        color += HairLight(light, n, t, v, albedo, strand, specScale);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                        color += HairLight(light, n, t, v, albedo, strand, specScale);
                    LIGHT_LOOP_END
                #endif

                color = MixFog(color, InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor));
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Universal Render Pipeline/Lit"
}
