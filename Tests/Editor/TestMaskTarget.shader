// Test-only shader with typical texture properties, used by TargetResolverTests.
Shader "Hidden/Archura/UVMaskPainter/Tests/MaskTarget"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base", 2D) = "white" {}
        _SplatControl ("Splat Control", 2D) = "black" {}
        [ArchuraMask] _Wear ("Wear", 2D) = "black" {}
        [HideInInspector] _HiddenMask ("Hidden", 2D) = "black" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag() : SV_Target { return 1; }
            ENDCG
        }
    }
}
