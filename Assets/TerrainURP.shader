Shader "Custom/Terrain_Triplanar_Final"
{
    Properties
    {
        _SandTex("Sand", 2D) = "white" {}
        _GrassTex("Grass", 2D) = "white" {}
        _RockTex("Rock", 2D) = "white" {}
        _SnowTex("Snow", 2D) = "white" {}
        _Tiling("Tiling", Float) = 0.05
        _Blend("Triplanar Blend", Range(1, 50)) = 10.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD3;
            };

            sampler2D _SandTex, _GrassTex, _RockTex, _SnowTex;
            float _Tiling, _Blend;

            v2f vert(appdata v) {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            half4 TriSample(sampler2D t, float3 p, float3 n, float til) {
                float3 b = pow(abs(n), _Blend);
                b /= (b.x + b.y + b.z);
                return tex2D(t, p.zy * til) * b.x + tex2D(t, p.xz * til) * b.y + tex2D(t, p.xy * til) * b.z;
            }

            half4 frag(v2f i) : SV_Target {
                float3 n = normalize(i.normalWS);
                
                half4 s = TriSample(_SandTex, i.positionWS, n, _Tiling);
                half4 g = TriSample(_GrassTex, i.positionWS, n, _Tiling);
                half4 r = TriSample(_RockTex, i.positionWS, n, _Tiling);
                half4 sn = TriSample(_SnowTex, i.positionWS, n, _Tiling);

                half4 finalColor = s * i.color.r + g * i.color.g + r * i.color.b + sn * i.color.a;

                Light l = GetMainLight();
                float diff = saturate(dot(n, l.direction)) * l.shadowAttenuation;
                float3 lighting = l.color * (diff + 0.35);

                return half4(finalColor.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}