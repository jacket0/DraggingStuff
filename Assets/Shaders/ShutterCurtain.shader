Shader "Custom/ShutterCurtain"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.73, 0.76, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.45
        _Metallic ("Metallic", Range(0, 1)) = 0.6
        _ClipHeight ("Clip Height", Float) = 1000
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        float _ClipHeight;

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            clip(_ClipHeight - input.worldPos.y);
            output.Albedo = _Color.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness;
            output.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
