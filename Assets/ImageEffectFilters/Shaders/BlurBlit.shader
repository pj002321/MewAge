// Graphics.Blit 전용 블러 셰이더 (UI Material 아님)
Shader "Hidden/BlurBlit"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 수평 블러
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment fragH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.uv  = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2 - 1, 0, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.uv.y = 1 - o.uv.y;
                #endif
                return o;
            }

            static const float W[7] = { 0.0625, 0.125, 0.1875, 0.25, 0.1875, 0.125, 0.0625 };

            half4 fragH(Varyings i) : SV_Target
            {
                half4 col = 0;
                UNITY_UNROLL
                for (int k = 0; k < 7; k++)
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        i.uv + float2(_MainTex_TexelSize.x * (k - 3), 0)) * W[k];
                return col;
            }
            ENDHLSL
        }

        Pass // 수직 블러
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment fragV
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.uv  = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2 - 1, 0, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.uv.y = 1 - o.uv.y;
                #endif
                return o;
            }

            static const float W[7] = { 0.0625, 0.125, 0.1875, 0.25, 0.1875, 0.125, 0.0625h };

            half4 fragV(Varyings i) : SV_Target
            {
                half4 col = 0;
                UNITY_UNROLL
                for (int k = 0; k < 7; k++)
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                        i.uv + float2(0, _MainTex_TexelSize.y * (k - 3))) * W[k];
                return col;
            }
            ENDHLSL
        }
    }
}
