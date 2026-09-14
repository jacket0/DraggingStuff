Shader "Game/Shelf Item Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.65, 0.05, 0.9)
        _WidthPixels ("Width In Pixels", Range(1, 4)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "DisableBatching" = "True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref 1
            ReadMask 1
            WriteMask 1
            Comp NotEqual
            Pass Replace
        }

        CGINCLUDE
        #include "UnityCG.cginc"

        fixed4 _Color;
        float _WidthPixels;

        struct appdata
        {
            float4 vertex : POSITION;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
        };

        v2f OffsetSilhouette(appdata input, float2 direction)
        {
            v2f output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.vertex.xy += direction * (2.0 * _WidthPixels / _ScreenParams.xy) * output.vertex.w;
            return output;
        }

        v2f MaskVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(0, 0));
        }

        v2f RightVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(1, 0));
        }

        v2f LeftVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(-1, 0));
        }

        v2f UpVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(0, 1));
        }

        v2f DownVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(0, -1));
        }

        v2f UpperRightVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(0.70710678, 0.70710678));
        }

        v2f UpperLeftVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(-0.70710678, 0.70710678));
        }

        v2f LowerRightVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(0.70710678, -0.70710678));
        }

        v2f LowerLeftVertex(appdata input)
        {
            return OffsetSilhouette(input, float2(-0.70710678, -0.70710678));
        }

        fixed4 OutlineColor(v2f input) : SV_Target
        {
            return _Color;
        }
        ENDCG

        Pass
        {
            Name "SilhouetteMask"
            ColorMask 0
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
            }
            CGPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "Right"
            CGPROGRAM
            #pragma vertex RightVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "Left"
            CGPROGRAM
            #pragma vertex LeftVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "Up"
            CGPROGRAM
            #pragma vertex UpVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "Down"
            CGPROGRAM
            #pragma vertex DownVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "UpperRight"
            CGPROGRAM
            #pragma vertex UpperRightVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "UpperLeft"
            CGPROGRAM
            #pragma vertex UpperLeftVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "LowerRight"
            CGPROGRAM
            #pragma vertex LowerRightVertex
            #pragma fragment OutlineColor
            ENDCG
        }

        Pass
        {
            Name "LowerLeft"
            CGPROGRAM
            #pragma vertex LowerLeftVertex
            #pragma fragment OutlineColor
            ENDCG
        }
    }
}
