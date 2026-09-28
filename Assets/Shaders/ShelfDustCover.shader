Shader "Custom/ShelfDustCover"
{
    Properties
    {
        _Color ("Color", Color) = (0.50, 0.46, 0.42, 0.62)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Vector) = (2, 1, 0, 0)
        _Reveal ("Reveal", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.3)) = 0.04
        _BandColor ("Band Color", Color) = (1, 0.96, 0.86, 0.9)
        _BandWidth ("Band Width", Range(0.01, 0.5)) = 0.12
        _BorderFade ("Border Fade", Vector) = (0.04, 0.08, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            sampler2D _NoiseTex;
            float4 _NoiseScale;
            float _Reveal;
            float _EdgeSoftness;
            fixed4 _BandColor;
            float _BandWidth;
            float4 _BorderFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float noise = tex2D(_NoiseTex, uv * _NoiseScale.xy).r;

                fixed4 color = _Color;
                color.rgb *= lerp(0.85, 1.15, noise);
                color.a *= lerp(0.75, 1.0, noise);

                float border = smoothstep(0.0, _BorderFade.x, min(uv.x, 1.0 - uv.x)) * smoothstep(0.0, _BorderFade.y, min(uv.y, 1.0 - uv.y));

                float boundary = lerp(-_EdgeSoftness - _BandWidth, 1.0 + _EdgeSoftness, _Reveal);
                float covered = smoothstep(boundary - _EdgeSoftness, boundary + _EdgeSoftness, uv.x);
                float bandCenter = boundary + _BandWidth * 0.5;
                float band = saturate(1.0 - abs(uv.x - bandCenter) / (_BandWidth * 0.5));
                band *= step(0.0001, _Reveal) * step(_Reveal, 0.9999);

                color.rgb = lerp(color.rgb, _BandColor.rgb, band * _BandColor.a);
                color.a = max(color.a, band * _BandColor.a) * covered * border;
                return color;
            }
            ENDCG
        }
    }
}
