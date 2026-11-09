// Unlit sprite for light pools, headlight cones, mission beams and combat effects.
// Additive by default; set _DstBlend = 10 (OneMinusSrcAlpha) for smoke.
// Works on Built-in and URP (plain CG/HLSL, no lighting). Fog-aware.
Shader "NeonHarbor/Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint (alpha = strength)", Color) = (1,1,1,1)
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Offset -2, -2
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _TintColor;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                fixed4 c = fixed4(_TintColor.rgb * t.rgb * 2.0, t.a * _TintColor.a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
}
