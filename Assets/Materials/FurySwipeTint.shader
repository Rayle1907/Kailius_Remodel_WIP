Shader "Kailius/FurySwipeTint"
{
    Properties
    {
        _MainTex ("Original attack sprite", 2D) = "white" {}
        _TintColor ("Soft red", Color) = (1, 0.45, 0.45, 1)
        _TintStrength ("Tint strength", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _TintColor;
            float _TintStrength;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float4 original = tex2D(_MainTex, input.uv);
                return float4(lerp(original.rgb, _TintColor.rgb, _TintStrength), original.a);
            }
            ENDHLSL
        }
    }
}
