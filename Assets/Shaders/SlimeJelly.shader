Shader "Custom/SlimeJelly"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 1, 0.1, 0.85)
        _RimColor ("Rim Color", Color) = (0.9, 1, 0.6, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.55
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightPower ("Highlight Power", Range(4, 128)) = 48
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.8
        _WobbleAmount ("Wobble Amount", Range(0, 0.02)) = 0.004
        _WobbleSpeed ("Wobble Speed", Range(0, 8)) = 2.2
        _WobbleFrequency ("Wobble Frequency", Range(1, 120)) = 40
        _DissolveScale ("Dissolve Scale", Range(10, 200)) = 70
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
            fixed4 _RimColor;
            float _RimPower;
            float _RimStrength;
            fixed4 _HighlightColor;
            float _HighlightPower;
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
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower);
                float highlight = pow(saturate(dot(normal, normalize(lightDirection + viewDirection))), _HighlightPower) * _HighlightStrength;

                fixed4 color = _Color;
                color.rgb = lerp(color.rgb, _RimColor.rgb, rim * _RimStrength) + _HighlightColor.rgb * highlight;
                color.a = saturate(color.a + rim * 0.25 + highlight);

                float noise = Hash(floor(input.objectPosition * _DissolveScale));
                float visible = smoothstep(_Reveal - 0.12, _Reveal, noise) * step(_Reveal, 0.999);
                color.a *= visible;
                return color;
            }
            ENDCG
        }
    }
}
