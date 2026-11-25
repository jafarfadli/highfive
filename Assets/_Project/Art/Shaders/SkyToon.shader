// The sky of Be Korean!: a golden-hour gradient (deep blue zenith, warm haze at the horizon), a sun with a
// soft glow, and big toon clouds drifting on a dome above the street. The clouds are cut from fractal noise
// with a crisp edge, shaded in two flat tones (sunlit and shade, with a warm rim on the side towards the sun
// and halftone dots in the shade) and outlined in ink, so they match the comic world below. LookKit sets the
// sun direction from the scene's sun.
Shader "SeoulFirstWeek/Sky Toon"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.18, 0.39, 0.75, 1)
        _SkyColor ("Sky", Color) = (0.44, 0.65, 0.91, 1)
        _HorizonColor ("Horizon haze", Color) = (0.97, 0.79, 0.6, 1)
        _GroundColor ("Below the horizon", Color) = (0.55, 0.45, 0.4, 1)
        _SunDirection ("Towards the sun", Vector) = (-0.5, 0.45, -0.7, 0)
        [HDR] _SunColor ("Sun", Color) = (1, 0.9, 0.68, 1)
        _SunSize ("Sun disc (cosine of its radius)", Range(0.995, 1)) = 0.9991
        _SunGlow ("Sun glow", Range(0, 2)) = 0.9
        _CloudLit ("Cloud, sunlit", Color) = (1, 0.97, 0.92, 1)
        _CloudWarm ("Cloud, rim towards the sun", Color) = (1, 0.79, 0.55, 1)
        _CloudShade ("Cloud, shade", Color) = (0.66, 0.7, 0.86, 1)
        _CloudInk ("Cloud ink", Color) = (0.22, 0.18, 0.24, 1)
        _CloudCover ("Cover", Range(0, 1)) = 0.5
        _CloudScale ("Scale", Float) = 1.4
        _CloudSpeed ("Drift", Float) = 0.006
        _InkWidth ("Ink line (px)", Float) = 2.2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _SkyColor, _HorizonColor, _GroundColor;
                float4 _SunDirection;
                half4 _SunColor;
                float _SunSize;
                half _SunGlow;
                half4 _CloudLit, _CloudWarm, _CloudShade, _CloudInk;
                float _CloudCover, _CloudScale, _CloudSpeed, _InkWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++)
                {
                    s += a * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return s;
            }

            // big puffy masses: a slow coverage field with billows on top
            float Density(float2 uv)
            {
                return Fbm(uv * 0.55) * 0.78 + Fbm(uv * 1.9 + 3.7) * 0.34;
            }

            // halftone dots on a 45° screen, 7 px at 1080p; 1 inside a dot
            half Dots(float2 positionCS, half density)
            {
                float scale = max(_ScreenParams.y / 1080.0, 0.5);
                float2 p = positionCS / (7.0 * scale);
                float2 r = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                float d = length(frac(r) - 0.5);
                float rad = sqrt(saturate(density)) * 0.62;
                float aa = max(fwidth(d), 1e-4) * 0.9;
                return 1.0h - smoothstep(rad - aa, rad + aa, d);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float h = d.y;

                // the sky: warm haze at the horizon, blue above, deep blue at the zenith
                half3 col = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, 0.28, h));
                col = lerp(col, _ZenithColor.rgb, smoothstep(0.28, 0.95, h));
                col = lerp(col, _GroundColor.rgb, smoothstep(0.0, -0.1, h));

                // the sun: a soft glow, warmest in the haze, and a crisp disc bright enough to bloom
                float cs = dot(d, sun);
                col += _SunColor.rgb * _SunGlow * (pow(saturate(cs), 96.0) * 0.7 + pow(saturate(cs), 6.0) * 0.22 * (1.0 - smoothstep(0.0, 0.5, h)));
                float sunAa = max(fwidth(cs), 1e-6);
                col = lerp(col, _SunColor.rgb * 3.0, smoothstep(_SunSize - sunAa, _SunSize + sunAa, cs));

                // clouds on a dome: flat toon tones, a warm rim, an ink edge
                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.12) * _CloudScale + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    float dens = Density(uv);
                    float px = max(fwidth(dens), 1e-5);
                    float threshold = 1.0 - _CloudCover;
                    float fade = smoothstep(0.02, 0.2, h);
                    float mask = smoothstep(threshold - px * 0.5, threshold + px * 0.5, dens) * fade;

                    // the side towards the sun is where the density falls off towards it
                    float2 toSun = normalize(sun.xz + 1e-4) * 0.35;
                    float ahead = Density(uv + toSun);
                    half lit = smoothstep(-0.012, 0.012, dens - ahead + 0.005);
                    half rim = lit * (1.0 - smoothstep(threshold + 0.01, threshold + 0.05, dens));
                    half3 cloud = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                    cloud = lerp(cloud, _CloudWarm.rgb, rim * saturate(cs * 0.5 + 0.7));
                    cloud *= 1.0h - 0.12h * Dots(i.positionCS.xy, (1.0h - lit) * 0.5h);

                    float lineHalf = _InkWidth * 0.5 * px;
                    half edge = 1.0h - smoothstep(lineHalf - px * 0.5, lineHalf + px * 0.5, abs(dens - threshold));
                    col = lerp(col, cloud, mask);
                    col = lerp(col, _CloudInk.rgb, edge * fade * 0.9h);
                }
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
