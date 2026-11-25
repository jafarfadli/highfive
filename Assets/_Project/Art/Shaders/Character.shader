// People: one cel-shaded material for every body, garment and face (three flat tones of light with
// halftone dots in the shade on clothes, InkLighting.hlsl). Each vertex says what it is made of:
//   COLOR      garment or skin tint (linear), alpha = baked ambient occlusion
//   TEXCOORD0  fabric UVs in texture repeats (face UVs: front projection of the head, 0..1)
//   TEXCOORD1  x = texture-array slice, y = type (0 cloth, 1 skin, 2 face, 3 soft cloth with sheen)
// Fabrics are neutral textures tinted by the vertex colour; faces are painted and the face alpha says how
// much of the skin tint applies (eyes, brows and lips keep their own colour). Normal maps need no mesh
// tangents (cotangent frame from screen derivatives). Skin gets wrap lighting with a warm terminator
// (cheap subsurface scattering); soft fabrics get a velvety rim. Reuses the URP Lit shadow/depth passes.
Shader "SeoulFirstWeek/Character"
{
    Properties
    {
        [NoScaleOffset] _FabricArray ("Fabrics (A = smoothness)", 2DArray) = "" {}
        [NoScaleOffset] _NormalArray ("Fabric normals (RGB)", 2DArray) = "" {}
        [NoScaleOffset] _FaceArray ("Faces (A = skin tint)", 2DArray) = "" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _BumpScale ("Normal strength", Float) = 1
        _Smoothness ("Smoothness scale", Range(0, 2)) = 1

        // URP Lit inputs read by the reused shadow and depth passes
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
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "InkLighting.hlsl"

            TEXTURE2D_ARRAY(_FabricArray); SAMPLER(sampler_FabricArray);
            TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_FaceArray); SAMPLER(sampler_FaceArray);

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
                nointerpolation float2 mat : TEXCOORD4;
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
                o.mat = v.mat;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // tangent frame from screen-space derivatives (no mesh tangents needed)
            float3x3 CotangentFrame(float3 n, float3 p, float2 uv)
            {
                float3 dp1 = ddx(p), dp2 = ddy(p);
                float2 duv1 = ddx(uv), duv2 = ddy(uv);
                float3 dp2perp = cross(dp2, n), dp1perp = cross(n, dp1);
                float3 t = dp2perp * duv1.x + dp1perp * duv2.x;
                float3 b = dp2perp * duv1.y + dp1perp * duv2.y;
                float invmax = rsqrt(max(max(dot(t, t), dot(b, b)), 1e-12));
                return float3x3(t * invmax, b * invmax, n);
            }

            // light that bleeds past the terminator through skin: one flat warm band, drawn like a blush line
            // on a comic face rather than a soft gradient
            half3 SkinScatter(Light light, half3 n, half3 albedo)
            {
                half nl = dot(n, light.direction);
                half band = smoothstep(-0.2h, -0.16h, nl) * (1.0h - smoothstep(0.04h, 0.08h, nl));
                return albedo * half3(0.32h, 0.1h, 0.06h) * band * light.shadowAttenuation * light.distanceAttenuation * light.color;
            }

            // velvety rim on knits, wool, corduroy and fleece
            half3 ClothSheen(Light light, half3 n, half3 viewDir, half3 albedo)
            {
                half nl = saturate(dot(n, light.direction) * 0.6h + 0.4h);
                half rim = pow(1.0h - saturate(dot(n, viewDir)), 4.0h);
                return rim * nl * light.shadowAttenuation * light.distanceAttenuation * light.color * (albedo * 0.5h + 0.12h);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                uint slice = (uint)round(i.mat.x);
                int type = (int)round(i.mat.y);
                bool face = type == 2;
                bool skin = type == 1 || face;
                float3 n0 = normalize(i.normalWS);

                half4 tex;
                half3 nTS;
                if (face)
                {
                    tex = SAMPLE_TEXTURE2D_ARRAY(_FaceArray, sampler_FaceArray, i.uv, slice);
                    // skin grain from the skin fabric, scaled to the face projection
                    nTS = UnpackNormalRGB(SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, i.uv * 2.0, 0), _BumpScale * 0.6);
                }
                else
                {
                    tex = SAMPLE_TEXTURE2D_ARRAY(_FabricArray, sampler_FabricArray, i.uv, slice);
                    nTS = UnpackNormalRGB(SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, i.uv, slice), _BumpScale * (skin ? 0.6 : 1.0));
                }

                half3 tint = i.color.rgb * _BaseColor.rgb;
                half ao = i.color.a;
                half3 albedo;
                half smoothness;
                if (face)
                {
                    albedo = lerp(tex.rgb, tex.rgb * tint, tex.a);
                    smoothness = 0.36h;
                }
                else
                {
                    albedo = tex.rgb * tint;
                    smoothness = tex.a;
                }
                albedo *= lerp(0.55h, 1.0h, ao);

                float3x3 tbn = CotangentFrame(n0, i.positionWS, i.uv);
                float3 normalWS = normalize(mul(nTS, tbn));

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                InkSurface s;
                s.albedo = albedo;
                s.metallic = 0;
                s.smoothness = saturate(smoothness * _Smoothness);
                s.occlusion = ao;
                s.halftone = skin ? 0.0h : 0.8h; // faces and hands stay clean
                half4 color = InkFragment(inputData, s);

                // skin keeps a warm terminator (light bleeding through), from every light
                if (skin)
                {
                    half3 extra = 0;
                    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                    extra += SkinScatter(mainLight, normalWS, albedo);
                    #if defined(_ADDITIONAL_LIGHTS)
                        uint pixelLightCount = GetAdditionalLightsCount();
                        #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                            extra += SkinScatter(light, normalWS, albedo);
                        }
                        #endif
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                            extra += SkinScatter(light, normalWS, albedo);
                        LIGHT_LOOP_END
                    #endif
                    color.rgb += extra * 0.6h;
                }
                else if (type == 3)
                {
                    // soft fabrics: a flat velvety rim
                    half rim = smoothstep(0.62h, 0.7h, 1.0h - saturate(dot(normalWS, inputData.viewDirectionWS)));
                    color.rgb += rim * inputData.bakedGI * (albedo * 0.5h + 0.12h) * ao;
                }

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Universal Render Pipeline/Lit"
}
