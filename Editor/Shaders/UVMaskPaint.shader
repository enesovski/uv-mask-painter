// Editor-only shader used by UV Mask Painter to paint masks on the GPU.
// Mesh passes rasterize the mesh in UV space (one fragment per mask texel) and measure the brush
// in world space, so strokes are continuous across UV seams and never leak into other islands.
// Render-pipeline independent: it is only used for off-screen rendering via command buffers.
Shader "Hidden/Archura/UVMaskPainter/Paint"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _CoverageTex;
    sampler2D _OccupancyTex;
    float4 _TexelSize;       // (1/width, 1/height, width, height) of the target
    float3 _BrushCenter;
    float3 _BrushNormal;
    float _BrushRadius;
    float _BrushHardness;
    float _BrushPressure;
    float4 _Value;
    float _Strength;

    // ---------- UV-space mesh rasterization ----------
    struct MeshInput
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
        float2 uv2 : TEXCOORD2;
        float2 uv3 : TEXCOORD3;
    };

    struct MeshVaryings
    {
        float4 position : SV_POSITION;
        float3 worldPos : TEXCOORD0;
        float3 worldNormal : TEXCOORD1;
    };

    MeshVaryings VertUVSpace(MeshInput v)
    {
        #if defined(_UV1)
            float2 uv = v.uv1;
        #elif defined(_UV2)
            float2 uv = v.uv2;
        #elif defined(_UV3)
            float2 uv = v.uv3;
        #else
            float2 uv = v.uv0;
        #endif

        MeshVaryings o;
        o.position = float4(uv * 2.0 - 1.0, 0.5, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            o.position.y = -o.position.y;
        #endif
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        o.worldNormal = UnityObjectToWorldNormal(v.normal);
        return o;
    }

    // Brush falloff: 1 inside the hard core, smooth to 0 at the radius.
    float FragCoverage(MeshVaryings i) : SV_Target
    {
        float distance01 = length(i.worldPos - _BrushCenter) / _BrushRadius;
        float falloff = saturate((1.0 - distance01) / max(1.0 - _BrushHardness, 1e-4));
        falloff = falloff * falloff * (3.0 - 2.0 * falloff);

        // Don't paint through thin geometry: skip texels facing away from the stroke.
        float normalLength = length(i.worldNormal);
        if (normalLength > 1e-5 && dot(i.worldNormal / normalLength, _BrushNormal) < -0.05)
            falloff = 0.0;

        return falloff * _BrushPressure;
    }

    float FragOccupancy(MeshVaryings i) : SV_Target
    {
        return 1.0;
    }

    // ---------- Full-texture passes ----------
    // Drawn as a 0..1 UV quad through the same UV-space mapping as the mesh passes,
    // so they can be limited to the painted region with a scissor rect.
    struct QuadInput
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct QuadVaryings
    {
        float4 position : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    QuadVaryings VertQuad(QuadInput v)
    {
        QuadVaryings o;
        o.position = float4(v.uv * 2.0 - 1.0, 0.5, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            o.position.y = -o.position.y;
        #endif
        o.uv = v.uv;
        return o;
    }

    float4 FragDilate(QuadVaryings i) : SV_Target
    {
        float coverage = tex2Dlod(_MainTex, float4(i.uv, 0, 0)).r;
        if (tex2Dlod(_OccupancyTex, float4(i.uv, 0, 0)).r > 0.5)
            return coverage;

        // Outside every UV island: extend nearby paint by a few texels so seams don't show
        // when the mask is filtered or mip-mapped.
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                float2 offset = float2(x, y) * radius * _TexelSize.xy;
                coverage = max(coverage, tex2Dlod(_MainTex, float4(i.uv + offset, 0, 0)).r);
            }
        }
        return coverage;
    }

    float4 FragComposite(QuadVaryings i) : SV_Target
    {
        float4 base = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
        float amount = saturate(tex2Dlod(_CoverageTex, float4(i.uv, 0, 0)).r) * _Strength;
        return lerp(base, _Value, amount);
    }

    float4 FragCompositeSmooth(QuadVaryings i) : SV_Target
    {
        float4 base = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
        float amount = saturate(tex2Dlod(_CoverageTex, float4(i.uv, 0, 0)).r) * _Strength;

        // 5x5 Gaussian (binomial weights).
        static const float weights[5] = { 1, 4, 6, 4, 1 };
        float4 sum = 0;
        for (int x = -2; x <= 2; x++)
        for (int y = -2; y <= 2; y++)
        {
            float w = weights[x + 2] * weights[y + 2];
            sum += w * tex2Dlod(_MainTex, float4(i.uv + float2(x, y) * _TexelSize.xy, 0, 0));
        }
        return lerp(base, sum / 256.0, amount);
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // 0: Brush coverage (max blend so overlapping dabs never exceed the stroke opacity)
        Pass
        {
            Name "Coverage"
            BlendOp Max
            Blend One One
            ColorMask R
            CGPROGRAM
            #pragma vertex VertUVSpace
            #pragma fragment FragCoverage
            #pragma multi_compile_local _ _UV1 _UV2 _UV3
            ENDCG
        }

        // 1: Occupancy (which texels belong to a triangle)
        Pass
        {
            Name "Occupancy"
            ColorMask R
            CGPROGRAM
            #pragma vertex VertUVSpace
            #pragma fragment FragOccupancy
            #pragma multi_compile_local _ _UV1 _UV2 _UV3
            ENDCG
        }

        // 2: Dilate coverage into the gutters between islands
        Pass
        {
            Name "Dilate"
            CGPROGRAM
            #pragma vertex VertQuad
            #pragma fragment FragDilate
            ENDCG
        }

        // 3: Blend the stroke onto the pre-stroke snapshot
        Pass
        {
            Name "Composite"
            ColorMask [_ColorMask]
            CGPROGRAM
            #pragma vertex VertQuad
            #pragma fragment FragComposite
            ENDCG
        }

        // 4: Blend a blurred snapshot (smooth brush)
        Pass
        {
            Name "CompositeSmooth"
            ColorMask [_ColorMask]
            CGPROGRAM
            #pragma vertex VertQuad
            #pragma fragment FragCompositeSmooth
            ENDCG
        }
    }
}
