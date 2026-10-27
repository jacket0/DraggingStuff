Shader "Custom/SlimeJelly"
{
    Properties
    {
        _Color ("Top Color", Color) = (0.7, 0.81, 0.35, 0.94)
        _EdgeColor ("Bottom Color", Color) = (0.35, 0.56, 0.16, 0.96)
        _HeightScale ("Height Scale", Range(0.005, 0.2)) = 0.02
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.35
        _SelfLight ("Self Light", Range(0, 1)) = 0.25
        _OutlineColor ("Outline Color", Color) = (0.16, 0.33, 0.06, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.02)) = 0.006
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightPower ("Highlight Power", Range(4, 256)) = 22
        _HighlightCut ("Highlight Cut", Range(0.3, 0.99)) = 0.72
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.7
        _HighlightDirection ("Highlight Direction", Vector) = (0.35, 1, 0.8, 0)
        _WobbleAmount ("Wobble Amount", Range(0, 0.02)) = 0.002
        _WobbleSpeed ("Wobble Speed", Range(0, 8)) = 2.2
        _WobbleFrequency ("Wobble Frequency", Range(1, 120)) = 40
        _DissolveScale ("Dissolve Scale", Range(10, 400)) = 120
        _Reveal ("Reveal", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        CGINCLUDE
        #include "UnityCG.cginc"

        float _WobbleAmount;
        float _WobbleSpeed;
        float _WobbleFrequency;
        float _DissolveScale;
        float _Reveal;

        float3 Wobble(float3 position, float3 normal)
        {
            float phase = _Time.y * _WobbleSpeed + dot(position, float3(1.0, 1.3, 0.7)) * _WobbleFrequency;
            return position + normal * sin(phase) * _WobbleAmount;
        }

        float Visibility(float3 objectPosition)
        {
            float noise = frac(sin(dot(floor(objectPosition * _DissolveScale), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            return smoothstep(_Reveal - 0.12, _Reveal, noise) * step(_Reveal, 0.999);
        }
        ENDCG

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPosition : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float3 normal = normalize(input.normal);
                float3 position = Wobble(input.vertex.xyz, normal) + normal * _OutlineWidth;
                output.vertex = UnityObjectToClipPos(float4(position, 1.0));
                output.objectPosition = input.vertex.xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = _OutlineColor;
                color.a *= Visibility(input.objectPosition) * step(0.0001, _OutlineWidth);
                return color;
            }
            ENDCG
        }

        Cull Back
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Jelly alpha:fade vertex:vert noshadow nolightmap nodynlightmap nometa

        fixed4 _Color;
        fixed4 _EdgeColor;
        float _HeightScale;
        float _ShadeStrength;
        float _SelfLight;
        fixed4 _HighlightColor;
        float _HighlightPower;
        float _HighlightCut;
        float _HighlightStrength;
        float4 _HighlightDirection;

        struct Input
        {
            float3 objectPosition;
            float3 worldNormal;
            float3 viewDir;
        };

        void vert(inout appdata_full input, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.objectPosition = input.vertex.xyz;
            input.vertex.xyz = Wobble(input.vertex.xyz, input.normal);
        }

        half4 LightingJelly(SurfaceOutput surface, half3 lightDirection, half3 viewDirection, half attenuation)
        {
            half wrap = saturate(dot(surface.Normal, lightDirection) * 0.5 + 0.5);
            half diffuse = lerp(1.0 - _ShadeStrength, 1.0, wrap);
            half4 color;
            color.rgb = surface.Albedo * diffuse * _LightColor0.rgb * attenuation;
            color.a = surface.Alpha;
            return color;
        }

        void surf(Input input, inout SurfaceOutput output)
        {
            float thickness = smoothstep(0.0, 1.0, saturate(input.objectPosition.y / _HeightScale));
            fixed4 color = lerp(_EdgeColor, _Color, thickness);
            float3 normal = normalize(input.worldNormal);
            float3 halfVector = normalize(normalize(_HighlightDirection.xyz) + normalize(input.viewDir));
            float highlight = smoothstep(_HighlightCut, _HighlightCut + 0.08, pow(saturate(dot(normal, halfVector)), _HighlightPower)) * _HighlightStrength;
            output.Albedo = color.rgb * (1.0 - highlight);
            output.Emission = color.rgb * _SelfLight + _HighlightColor.rgb * highlight;
            output.Alpha = saturate(color.a + highlight) * Visibility(input.objectPosition);
        }
        ENDCG
    }
}
