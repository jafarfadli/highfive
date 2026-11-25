// Full-screen ink pass (URP Full Screen Pass renderer feature, after the opaque world and the sky, before
// transparents): draws the comic outlines and the paper grain over the rendered scene.
//   Silhouettes: the Laplacian of inverse depth, which is zero on any flat surface whatever its slope, so
//                ground planes at grazing angles stay clean while object edges against what is behind
//                them light up.
//   Creases:     where the scene normals turn sharply between neighbouring pixels (box edges, folds).
// Two line weights, as in a comic: a silhouette straddles its edge, about 2 px on each side near the camera
// (4–5 px in all at 1080p) thinning to 1 px a side by 20 m; creases are one pixel. Lines are a deep shade of
// the colour underneath (they keep the scene's colours) and fade with distance so the far street stays calm.
// A faint paper grain multiplies over all. InkKit.EnsureOutline sets the material's values.
Shader "SeoulFirstWeek/Ink Outline"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.09, 0.07, 0.06, 1)
        _InkStrength ("Line strength", Range(0, 1)) = 0.95
        _LineWidth ("Silhouette half-width near the camera (px at 1080p)", Range(0.5, 4)) = 2.2
        _DepthThreshold ("Silhouette threshold", Float) = 0.28
        _NormalThreshold ("Crease threshold", Range(0, 1)) = 0.32
        _FadeStart ("Fade start (m)", Float) = 30
        _FadeEnd ("Fade end (m)", Float) = 110
        _Grain ("Paper grain", Range(0, 0.2)) = 0.05
        [NoScaleOffset] _GrainTex ("Grain (R)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Ink"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_GrainTex);
            SAMPLER(sampler_GrainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half _InkStrength;
                float _LineWidth;
                float _DepthThreshold;
                half _NormalThreshold;
                float _FadeStart;
                float _FadeEnd;
                half _Grain;
            CBUFFER_END

            // inverse linear depth: linear in screen space on any plane (0 = sky)
            float InvDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 0.0) return 0.0;
                #else
                    if (raw >= 1.0) return 0.0;
                #endif
                return 1.0 / LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float res = max(_ScreenParams.y / 1080.0, 0.5);
                float2 onePx = res / _ScreenParams.xy;

                // the silhouette kernel widens up close: heavy outlines on what is near, lighter far away
                float c = InvDepth(uv);
                float eyeC = c > 0 ? 1.0 / c : 1e5;
                float2 px = onePx * lerp(_LineWidth, 1.0, saturate((eyeC - 3.0) / 17.0));
                float l = InvDepth(uv - float2(px.x, 0)), r = InvDepth(uv + float2(px.x, 0));
                float d = InvDepth(uv - float2(0, px.y)), u = InvDepth(uv + float2(0, px.y));
                // diagonals catch corners the cross misses
                float ld = InvDepth(uv - px), ru = InvDepth(uv + px);
                float lu = InvDepth(uv + float2(-px.x, px.y)), rd = InvDepth(uv + float2(px.x, -px.y));

                float nearest = max(max(max(c, l), max(r, d)), u);
                float eye = nearest > 0 ? 1.0 / nearest : 1e5;
                float lap = abs(l + r - 2 * c) + abs(u + d - 2 * c) + 0.5 * (abs(ld + ru - 2 * c) + abs(lu + rd - 2 * c));
                half silhouette = smoothstep(_DepthThreshold, _DepthThreshold * 2.0, lap * eye * 40.0);

                // creases: the thin weight, one pixel, from forward differences (only one side of the fold)
                half creases = 0;
                if (c > 0)
                {
                    half3 nc = SampleSceneNormals(uv);
                    half3 nr = SampleSceneNormals(uv + float2(onePx.x, 0));
                    half3 nu = SampleSceneNormals(uv + float2(0, onePx.y));
                    half turn = max(1 - dot(nc, nr), 1 - dot(nc, nu));
                    creases = smoothstep(_NormalThreshold, _NormalThreshold + 0.18h, turn) * 0.85h;
                }

                half fade = 1.0h - smoothstep(_FadeStart, _FadeEnd, eye);
                half edge = saturate(max(silhouette, creases)) * _InkStrength * lerp(0.35h, 1.0h, fade);

                // the line is a deep shade of what it outlines, pulled towards the ink
                half3 lineCol = lerp(scene.rgb * 0.16h, _InkColor.rgb, 0.55h);
                half3 col = lerp(scene.rgb, lineCol, edge);

                // paper grain, fixed to the screen like the paper the page is printed on
                half grain = SAMPLE_TEXTURE2D(_GrainTex, sampler_GrainTex, input.positionCS.xy / 512.0).r;
                col *= 1.0h - _Grain * (1.0h - grain) * 2.0h;
                return half4(col, scene.a);
            }
            ENDHLSL
        }
    }
}
