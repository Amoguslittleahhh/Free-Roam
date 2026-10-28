Shader "FreeRoam/ScopeGlass"
{
    // The glass at the ends of the SAR-21's optic: coated glass that reflects the sky at grazing angles, shows the scene through it only while the eye looks straight down the tube
    // (_Clarity), and has the reticle etched into it (thin wires, ring and dot drawn on the glass itself, so they share its reflections and its perspective).
    Properties
    {
        _MainTex ("Scene", 2D) = "black" {}
        _Clarity ("Clarity", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Glass"
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Clarity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 lp : TEXCOORD1; float3 nWS : TEXCOORD2; float3 pWS : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.pWS = p.positionWS;
                o.nWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv; o.lp = i.uv2;
                return o;
            }

            float Cover(float d, float w, float aa) { return 1.0 - smoothstep(w - aa, w + aa, d); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float3 v = normalize(_WorldSpaceCameraPos - i.pWS);
                if (dot(n, v) < 0.0) n = -n;
                float ndv = saturate(dot(n, v));
                float fres = 0.05 + 0.95 * pow(1.0 - ndv, 5.0);
                float3 refl = SampleSH(reflect(-v, n));
                float3 coat = float3(0.90, 0.95, 1.00);                         // neutral, very slightly cool: no coloured cast
                float3 sheen = min(refl, float3(0.6, 0.6, 0.6)) * coat * (fres * 0.5 + 0.01);

                float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float3 inside = float3(0.010, 0.011, 0.014);                    // the dark inside of the tube, seen from off the axis
                float3 col = lerp(inside, scene * float3(0.97, 0.99, 0.98), _Clarity);

                float r = length(i.lp);
                float aa = max(fwidth(r) * 0.8, 1e-4);
                float2 a = abs(i.lp);
                float arm = max(Cover(a.y, 0.0065, aa) * step(0.112, a.x), Cover(a.x, 0.0065, aa) * step(0.112, a.y)) * (1.0 - smoothstep(0.97, 0.99, r));
                float ring = Cover(abs(r - 0.085), 0.027, aa);
                float dotm = Cover(r, 0.011, aa);
                float etch = saturate(max(arm, max(ring, dotm)));
                col = lerp(col, float3(0.004, 0.004, 0.005), etch * 0.96);       // the etching is black, in the glass

                col += sheen * (1.0 - 0.6 * etch);
                col *= 1.0 - 0.55 * smoothstep(0.90, 1.0, r);                    // darkened rim of the glass
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
