Shader "Mewage/WeaponFireOutline"
{
    Properties
    {
        _MainTex        ("Sprite Texture", 2D)  = "white" {}
        _FireColorInner ("Inner Color",  Color) = (1.0, 0.95, 0.3, 1)
        _FireColorOuter ("Outer Color",  Color) = (1.0, 0.10, 0.0, 1)
        _FlickerSpeed   ("Flicker Speed", Float) = 4.0
        _FlickerScale   ("Flicker Scale", Float) = 10.0
        _Intensity      ("Intensity", Range(0, 2)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha One   // Additive: 무기 테두리에서 빛이 새어 나오는 효과
        ZWrite Off
        Cull Off

        Pass
        {
            Name "WeaponFireOutline"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _FireColorInner;
                half4  _FireColorOuter;
                float  _FlickerSpeed;
                float  _FlickerScale;
                float  _Intensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - 0.1);

                float t = _Time.y;

                // 2단 UV 왜곡 — 가로·세로 독립적으로 흔들려 불꽃처럼 너울거림
                float2 fuv = IN.uv;
                fuv.x += sin(t * 2.3  + IN.uv.y * 6.0 ) * 0.035
                       + sin(t * 4.1  + IN.uv.y * 11.0) * 0.015;
                fuv.y -= t * 0.22
                       + sin(t * 1.7  + IN.uv.x * 5.0 ) * 0.025;

                // 3 옥타브 노이즈
                float n;
                n  = sin(fuv.x * _FlickerScale        + t * _FlickerSpeed       ) * 0.45;
                n += sin(fuv.x * _FlickerScale * 2.4  - t * _FlickerSpeed * 1.6 + fuv.y * 2.5) * 0.32;
                n += sin(fuv.y * _FlickerScale * 1.1  + t * _FlickerSpeed * 1.1 ) * 0.23;
                float flicker = saturate(n * 0.5 + 0.5);

                // 제곱 커브로 어두운 부분을 더 극단적으로 → 대비 강화
                float f2 = flicker * flicker;

                half4 col = lerp(_FireColorOuter, _FireColorInner, f2);
                // 알파도 깜빡임에 따라 크게 변화 (0.2 ~ 1.0 범위)
                col.a = alpha * (0.2 + f2 * 0.8) * _Intensity;
                return col;
            }
            ENDHLSL
        }
    }
}
