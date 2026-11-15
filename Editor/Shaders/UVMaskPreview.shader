// Scene view mask overlay: tints the mesh by the painted channels (R red, G green, B blue, A white).
Shader "Hidden/Archura/UVMaskPainter/Preview"
{
    Properties
    {
        _MaskTex ("Mask", 2D) = "black" {}
        _Channels ("Channels", Vector) = (1, 0, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" }
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _UV1 _UV2 _UV3
            #include "UnityCG.cginc"

            sampler2D _MaskTex;
            float4 _Channels;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                #if defined(_UV1)
                    o.uv = v.uv1;
                #elif defined(_UV2)
                    o.uv = v.uv2;
                #elif defined(_UV3)
                    o.uv = v.uv3;
                #else
                    o.uv = v.uv0;
                #endif
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 m = tex2D(_MaskTex, i.uv) * _Channels;
                float3 tint = m.r * float3(1.0, 0.25, 0.2) + m.g * float3(0.3, 1.0, 0.35)
                            + m.b * float3(0.3, 0.55, 1.0) + m.a * float3(1.0, 1.0, 1.0);
                float amount = saturate(max(max(m.r, m.g), max(m.b, m.a)));
                float3 color = lerp(float3(0.05, 0.05, 0.07), saturate(tint), amount);
                return float4(color, _Opacity);
            }
            ENDCG
        }
    }
}
