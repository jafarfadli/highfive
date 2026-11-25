// Cel-shaded surface for everything static in Be Korean!: the texture is projected from three sides
// (triplanar) in metres, so any box, cylinder or floor gets the same texel density without UVs; with
// _UV_MAPPED it uses the mesh UVs instead (buildings and the pavement carry UVs in metres). Light comes in
// three flat tones with halftone dots in the shade (InkLighting.hlsl); the ink outlines are drawn later by
// the full-screen Ink pass. Albedo alpha holds smoothness. Uses the URP Lit material inputs, so the Lit
// shadow and depth passes are reused and the material stays SRP Batcher compatible.
//   _BaseMap tiling x = repeats per metre (1 / metres the texture covers), offset z = shift in metres.
Shader "SeoulFirstWeek/Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo (A = smoothness)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] [NoScaleOffset] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Toggle(_TRIPLANAR_OBJECT)] _ObjectSpace ("Project in object space", Float) = 0
        [Toggle(_UV_MAPPED)] _UVMapped ("Use mesh UVs (no triplanar)", Float) = 0
        _DetailAlbedoMapScale ("Halftone in the shade", Range(0, 1)) = 1

        // URP Lit inputs read by the reused shadow and depth passes
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _TRIPLANAR_OBJECT
            #pragma shader_feature_local _UV_MAPPED
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "InkLighting.hlsl"

            // the halftone strength rides in _DetailAlbedoMapScale: LitInput already declares it in the
            // UnityPerMaterial buffer, and a property of our own outside that buffer would break the SRP Batcher
            #define _Halftone _DetailAlbedoMapScale

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 mapPos : TEXCOORD1;     // where the texture is projected from, in metres (UV mode: uv in xy)
                float3 mapNormal : TEXCOORD2;  // surface normal in the same space
                half fogFactor : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;  // UV mode only
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                #if defined(_UV_MAPPED)
                    o.mapPos = float3(TRANSFORM_TEX(v.uv, _BaseMap), 0);
                    VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                    o.mapNormal = nrm.normalWS;
                    o.tangentWS = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                #elif defined(_TRIPLANAR_OBJECT)
                    float4x4 m = GetObjectToWorldMatrix();
                    float3 scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                    o.mapPos = v.positionOS.xyz * scale;
                    o.mapNormal = v.normalOS;
                #else
                    o.mapPos = pos.positionWS;
                    o.mapNormal = TransformObjectToWorldNormal(v.normalOS);
                #endif
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if defined(_UV_MAPPED)
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.mapPos.xy);
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.mapPos.xy), _BumpScale);
                float3 geomN = normalize(i.mapNormal);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(geomN, t) * i.tangentWS.w;
                float3 normalWS = normalize(nTS.x * t + nTS.y * b + nTS.z * geomN);
                #else
                // triplanar projection with the whiteout normal blend (after Ben Golus)
                float3 n = normalize(i.mapNormal);
                float3 p = i.mapPos * _BaseMap_ST.x + _BaseMap_ST.z;
                half3 blend = pow(abs(n), 4.0);
                blend /= dot(blend, 1.0);
                half3 axisSign = half3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
                float2 uvX = p.zy;
                float2 uvY = p.xz;
                float2 uvZ = p.xy;
                uvX.x *= axisSign.x;
                uvY.x *= axisSign.y;
                uvZ.x *= -axisSign.z;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX) * blend.x
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY) * blend.y
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ) * blend.z;

                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                tx.x *= axisSign.x;
                ty.x *= axisSign.y;
                tz.x *= -axisSign.z;
                half3 an = abs(n);
                tx = half3(tx.xy + n.zy, an.x * tx.z);
                ty = half3(ty.xy + n.xz, an.y * ty.z);
                tz = half3(tz.xy + n.xy, an.z * tz.z);
                float3 mapped = normalize(tx.zyx * blend.x + ty.xzy * blend.y + tz.xyz * blend.z);
                #if defined(_TRIPLANAR_OBJECT)
                    float3 normalWS = TransformObjectToWorldNormal(mapped);
                #else
                    float3 normalWS = mapped;
                #endif
                #endif

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
                s.albedo = albedo.rgb * _BaseColor.rgb;
                s.metallic = _Metallic;
                s.smoothness = albedo.a * _Smoothness;
                s.occlusion = 1;
                s.halftone = _Halftone;
                half4 color = InkFragment(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
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
