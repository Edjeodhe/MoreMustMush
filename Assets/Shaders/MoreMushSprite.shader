// Unlit sprite shader for SpriteRenderer / LineRenderer with the prototype's per-object effects:
// _Gold (golden variant tint), _Dark (silhouette), _Flash (white flash on hit).
Shader "MoreMush/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Gold ("Gold", Range(0,1)) = 0
        _Dark ("Dark", Range(0,1)) = 0
        _DarkColor ("Dark Color", Color) = (0.23,0.2,0.18,1)
        _Flash ("Flash", Range(0,1)) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend [_SrcBlend] [_DstBlend]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Gold, _Dark, _Flash;
            float4 _DarkColor;
            struct a2v { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (a2v v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * i.color;
                float l = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float3 gold = lerp(float3(0.55, 0.36, 0.02), float3(1.0, 0.92, 0.45), saturate(l * 1.4));
                c.rgb = lerp(c.rgb, gold, _Gold * 0.85);
                c.rgb = lerp(c.rgb, _DarkColor.rgb, _Dark);
                c.rgb = lerp(c.rgb, float3(1, 0.97, 0.85), _Flash);
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
