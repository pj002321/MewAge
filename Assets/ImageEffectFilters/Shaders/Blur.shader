Shader "Custom/Blur"
{
    Properties
    {
        _Size    ("Blur Size",  Range(0, 20)) = 4
        _MainTex ("MainTex",   2D)   = "white" {}
        _Color   ("Tint",      Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Size;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            static const half W[7] = { 0.0625h, 0.125h, 0.1875h, 0.25h, 0.1875h, 0.125h, 0.0625h };

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;

                // _ScreenParams: (width, height, 1+1/width, 1+1/height)
                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y) * _Size;

                half4 col = 0;
                UNITY_UNROLL
                for (int x = 0; x < 7; x++)
                {
                    UNITY_UNROLL
                    for (int y = 0; y < 7; y++)
                    {
                        float2 offset = float2(texelSize.x * (x - 3), texelSize.y * (y - 3));
                        col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture,
                                    sampler_CameraOpaqueTexture, uv + offset) * W[x] * W[y];
                    }
                }

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;
                return half4(col.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
