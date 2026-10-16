Shader "Custom/ShutterCurtainGlass"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.26, 0.3, 0.45)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.9
        _ClipHeight ("Clip Height", Float) = 1000
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _Color;
        half _Glossiness;
        float _ClipHeight;

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            clip(_ClipHeight - input.worldPos.y);
            output.Albedo = _Color.rgb;
            output.Smoothness = _Glossiness;
            output.Alpha = _Color.a;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
