// Fog / night overlay with soft holes around pinballs and the bar (prototype drawWeather OVL canvas).
// The quad covers the field rect (0,0)-(_Size) in prototype pixels; _Holes[i] = (x, y, radius, kind) with
// kind 0 = ball (1 → 0.8 at 55% → 0), kind 1 = bar (0.9 → 0).
Shader "MoreMush/WeatherMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (0.04,0.06,0.18,0.62)
        _Size ("Size", Vector) = (1920,1080,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _Size;
            float4 _Holes[65];
            int _Count;
            struct a2v { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float2 p = float2(i.uv.x * _Size.x, (1 - i.uv.y) * _Size.y);
                float a = _Color.a;
                for (int k = 0; k < 65; k++)
                {
                    if (k >= _Count) break;
                    float4 h = _Holes[k];
                    float t = saturate(length(p - h.xy) / h.z);
                    float e = h.w < 0.5 ? (t < 0.55 ? lerp(1, 0.8, t / 0.55) : lerp(0.8, 0, (t - 0.55) / 0.45)) : lerp(0.9, 0, t);
                    a *= 1 - e;
                }
                return float4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
