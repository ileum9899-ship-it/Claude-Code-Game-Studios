// Bloom (prefilter / downsample / upsample) + composite with ACES tone mapping,
// saturation, vignette and a flash overlay. Built-in render pipeline only.
Shader "Hidden/NeonHarbor/Post"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex;
    float4 _MainTex_TexelSize;
    float _Threshold, _Intensity, _Exposure, _Saturation, _Vignette;
    float4 _Flash;

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + 0.5, 0, 1); soft = soft * soft * 0.5;
        half contrib = max(soft, br - _Threshold) / max(br, 1e-5);
        return min(c * contrib, 30.0);
    }
    half3 ACES(half3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }

    half4 FragPrefilter(v2f_img i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
    half4 FragComposite(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        #ifdef UNITY_COLORSPACE_GAMMA
        c = GammaToLinearSpace(c);
        #endif
        c += tex2D(_BloomTex, i.uv).rgb * _Intensity;
        c = ACES(c * _Exposure);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation);
        float2 q = i.uv - 0.5; c *= 1.0 - _Vignette * dot(q, q) * 2.2;
        c = lerp(c, _Flash.rgb, _Flash.a);
        #ifdef UNITY_COLORSPACE_GAMMA
        c = LinearToGammaSpace(c);
        #endif
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragPrefilter
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragDown
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragUp
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragComposite
               ENDCG }
    }
}
