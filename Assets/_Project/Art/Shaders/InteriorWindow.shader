// Windows with a room behind them ("interior mapping"). The view ray is traced into a box behind
// the flat window quad: the back wall comes from a cell of a painted room atlas (RoomAtlas), and the
// floor, ceiling and side walls take their colour from that cell's bottom band, top band and edge
// columns. On top: blinds, a glass tint, a Fresnel reflection of sky and street, and fog.
// Per-window data written by FacadeDresser:
//   TEXCOORD1 = (atlas cell, lights 0..1, width m, height m)
//   TEXCOORD2 = (room depth m, blind 0..1 drawn from the top, 0, 0)
//   tangent   = the window's right direction, normal = out of the building
Shader "SeoulFirstWeek/Interior Window"
{
    Properties
    {
        _Atlas ("Room atlas", 2D) = "gray" {}
        _Cells ("Atlas columns, rows", Vector) = (4, 2, 0, 0)
        _Dim ("Room brightness, lights off", Range(0, 2)) = 0.3
        _Lit ("Room brightness, lights on", Range(0, 4)) = 1.3
        _SkyTop ("Reflected sky", Color) = (0.55, 0.67, 0.83, 1)
        _SkyHorizon ("Reflected horizon", Color) = (0.84, 0.85, 0.86, 1)
        _Street ("Reflected street", Color) = (0.36, 0.35, 0.34, 1)
        _Reflection ("Reflection straight on", Range(0, 1)) = 0.1
        _MaxReflection ("Reflection at grazing angles", Range(0, 1)) = 0.7
        _Tint ("Glass tint", Color) = (0.86, 0.92, 0.95, 1)
        _Blind ("Blind colour", Color) = (0.88, 0.86, 0.8, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Atlas_ST;
            float4 _Cells;
            half _Dim, _Lit, _Reflection, _MaxReflection;
            half4 _SkyTop, _SkyHorizon, _Street, _Tint, _Blind;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "InteriorWindow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            TEXTURE2D(_Atlas);
            SAMPLER(sampler_Atlas);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 room : TEXCOORD1;
                float4 extra : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewTS : TEXCOORD1;
                float4 room : TEXCOORD2;
                float4 extra : TEXCOORD3;
                float3 viewWS : TEXCOORD4;
                float3 normalWS : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float3 t = TransformObjectToWorldDir(v.tangentOS.xyz);
                float3 b = cross(n, t) * v.tangentOS.w;
                float3 view = positionWS - GetCameraPositionWS();
                o.viewTS = float3(dot(view, t), dot(view, b), dot(view, n));
                o.viewWS = view;
                o.normalWS = n;
                o.uv = v.uv;
                o.room = v.room;
                o.extra = v.extra;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 Cell(float cell, float2 uv, float2 dx, float2 dy)
            {
                float2 cells = max(_Cells.xy, 1);
                float2 index = float2(fmod(cell, cells.x), floor(cell / cells.x));
                float2 atlasUV = (index + clamp(uv, 0.01, 0.99)) / cells;
                return SAMPLE_TEXTURE2D_GRAD(_Atlas, sampler_Atlas, atlasUV, dx, dy).rgb;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float w = max(i.room.z, 0.1), h = max(i.room.w, 0.1), d = max(i.extra.x, 0.1);
                float cell = floor(i.room.x + 0.5);
                float2 dx = ddx(i.uv) / max(_Cells.xy, 1), dy = ddy(i.uv) / max(_Cells.xy, 1);

                // trace the view ray through the room box: x across [0, w], y up [0, h], z from 0 back to -d
                float3 dir = normalize(i.viewTS);
                dir.z = min(dir.z, -1e-3);
                float3 p = float3(i.uv.x * w, i.uv.y * h, 0);
                float3 inv = 1.0 / max(abs(dir), 1e-4);
                float tx = (dir.x > 0 ? w - p.x : p.x) * inv.x;
                float ty = (dir.y > 0 ? h - p.y : p.y) * inv.y;
                float tz = d * inv.z;
                float3 hit = p + dir * min(tz, min(tx, ty));
                float depth = saturate(-hit.z / d);

                half3 col;
                if (tz <= tx && tz <= ty)
                    col = Cell(cell, float2(hit.x / w, hit.y / h), dx, dy);
                else if (tx < ty)
                {
                    // side walls continue what stands at the edges of the back wall, running forward to the glass
                    float edge = 0.02 + 0.2 * (1.0 - depth);
                    col = Cell(cell, float2(dir.x > 0 ? 1.0 - edge : edge, hit.y / h), dx, dy) * lerp(0.7, 0.92, depth);
                }
                else if (dir.y < 0)
                    col = Cell(cell, float2(hit.x / w, 0.04), dx, dy) * lerp(0.62, 0.85, depth);
                else
                    col = Cell(cell, float2(hit.x / w, 0.97), dx, dy) * lerp(0.85, 1.0, depth);

                col *= lerp(_Dim, _Lit, saturate(i.room.y));

                // a blind pulled down from the top of the window
                float blind = i.extra.y;
                if (i.uv.y > 1.0 - blind)
                {
                    float slat = frac(i.uv.y * h / 0.045);
                    col = _Blind.rgb * (0.8 + 0.2 * smoothstep(0.0, 0.25, slat)) * lerp(0.7, 1.05, saturate(i.room.y));
                }
                col *= _Tint.rgb;

                // glass reflects the sky above and the street below the horizon
                float3 V = normalize(i.viewWS);
                float3 N = normalize(i.normalWS);
                float3 R = reflect(V, N);
                half facing = saturate(dot(-V, N));
                half fresnel = lerp(_Reflection, _MaxReflection, pow(1.0 - facing, 4.0));
                half3 sky = lerp(_SkyHorizon.rgb, _SkyTop.rgb, saturate(R.y * 1.6));
                half3 env = lerp(_Street.rgb, sky, smoothstep(-0.08, 0.1, R.y));
                col = lerp(col, env, fresnel);

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
