// UI ink: the material of every InkShape (panels, brush strips, balloons, marks, stamps, tickets).
// One RGBA noise texture drives three print effects, chosen per vertex so every shape batches together:
//   _MainTex.r  paper grain        (TEXCOORD0 = grain coordinates;  TEXCOORD1.x = grain amount)
//   _MainTex.g  dry-brush bristles (TEXCOORD2 = bristle coordinates; TEXCOORD1.y = dryness 0 solid .. 1 dry)
//   _MainTex.b  stamp grunge       (TEXCOORD0 again;                 TEXCOORD1.z = grunge amount)
//   TEXCOORD1.w halftone: screen-space ink dots, value = dot density (0 = solid fill)
// Supports RectMask2D clipping and stencil masks like UI/Default.
Shader "SeoulFirstWeek/UI Ink"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise (R grain, G bristle, B grunge)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Grain ("Paper grain strength", Range(0, 2)) = 1
        _DotPitch ("Halftone pitch (canvas px)", Float) = 9

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 grainUV : TEXCOORD0;
                float4 effects : TEXCOORD1;
                float2 bristleUV : TEXCOORD2;
                float4 worldPosition : TEXCOORD3;
                half4 mask : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float _Grain;
            float _DotPitch;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                o.grainUV = v.texcoord0.xy;
                o.effects = v.texcoord1;
                o.bristleUV = v.texcoord2.xy;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 c = i.color;
                half4 n = tex2D(_MainTex, i.grainUV) + _TextureSampleAdd;

                // paper grain: the fibres darken the fill a little
                c.rgb *= 1 - (1 - n.r) * i.effects.x * _Grain;

                // dry brush: bristle streaks open up where the stroke runs dry
                half bristle = tex2D(_MainTex, i.bristleUV).g;
                c.a *= saturate((bristle - i.effects.y + 0.25) * 5);

                // stamp grunge: ink missing in blotches
                half grunge = tex2D(_MainTex, i.grainUV * 2.3).b;
                c.a *= saturate((grunge - i.effects.z * 0.55) * 7 + (1 - i.effects.z));

                // halftone: round dots on a 45° screen, sized by the requested density
                if (i.effects.w > 0.001)
                {
                    float2 p = i.worldPosition.xy / _DotPitch;
                    float2 r = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                    float2 f = frac(r) - 0.5;
                    float d = length(f);
                    float rad = sqrt(saturate(i.effects.w)) * 0.62;
                    float aa = fwidth(d) * 1.2;
                    c.a *= 1 - smoothstep(rad - aa, rad + aa, d);
                }

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                c.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
