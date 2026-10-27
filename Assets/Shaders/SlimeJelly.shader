Shader "Custom/SlimeJelly"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.81, 0.35, 0.92)
        _EdgeColor ("Edge Color", Color) = (0.2, 0.4, 0.09, 0.96)
        _HeightScale ("Height Scale", Range(0.005, 0.2)) = 0.02
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.35
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightPower ("Highlight Power", Range(4, 256)) = 90
        _HighlightCut ("Highlight Cut", Range(0.3, 0.99)) = 0.62
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.7
        _WobbleAmount ("Wobble Amount", Range(0, 0.02)) = 0.002
        _WobbleSpeed ("Wobble Speed", Range(0, 8)) = 2.2
        _WobbleFrequency ("Wobble Frequency", Range(1, 120)) = 40
        _DissolveScale ("Dissolve Scale", Range(10, 400)) = 120
        _Reveal ("Reveal", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _HeightScale;
            float _ShadeStrength;
            fixed4 _HighlightColor;
            float _HighlightPower;
            float _HighlightCut;
            float _HighlightStrength;
            float _WobbleAmount;
            float _WobbleSpeed;
            float _WobbleFrequency;
            float _DissolveScale;
            float _Reveal;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                float3 objectPosition : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float phase = _Time.y * _WobbleSpeed + dot(input.vertex.xyz, float3(1.0, 1.3, 0.7)) * _WobbleFrequency;
                float3 displaced = input.vertex.xyz + input.normal * sin(phase) * _WobbleAmount;
                output.vertex = UnityObjectToClipPos(float4(displaced, 1.0));
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldPosition = mul(unity_ObjectToWorld, float4(displaced, 1.0)).xyz;
                output.objectPosition = input.vertex.xyz;
                return output;
            }

            float Hash(float3 cell)
            {
                return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);

                float thickness = smoothstep(0.0, 1.0, saturate(input.objectPosition.y / _HeightScale));
                float lighting = lerp(1.0 - _ShadeStrength, 1.0, saturate(dot(normal, lightDirection) * 0.5 + 0.5));
                fixed4 color = lerp(_EdgeColor, _Color, thickness);
                color.rgb *= lighting;

                float halfVector = pow(saturate(dot(normal, normalize(lightDirection + viewDirection))), _HighlightPower);
                float highlight = smoothstep(_HighlightCut, _HighlightCut + 0.08, halfVector) * _HighlightStrength;
                color.rgb = lerp(color.rgb, _HighlightColor.rgb, highlight);
                color.a = saturate(color.a + highlight);

                float noise = Hash(floor(input.objectPosition * _DissolveScale));
                float visible = smoothstep(_Reveal - 0.12, _Reveal, noise) * step(_Reveal, 0.999);
                color.a *= visible;
                return color;
            }
            ENDCG
        }
    }
}
