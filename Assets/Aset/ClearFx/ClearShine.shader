// 스프라이트 모양은 그대로 두고 색을 흐르는 무지개색과 반짝이는 빛줄기로 바꿔 보여 준다.
// _Fill이 0이면 원래 모습, 1이면 완전히 무지개색으로 빛난다.
Shader "ClearFx/ClearShine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Fill ("Fill", Range(0, 1)) = 1
        _HueSpeed ("Hue Speed", Float) = 0.2
        _HueScale ("Hue Scale (per world unit)", Float) = 0.04
        _Saturation ("Saturation", Range(0, 1)) = 0.75
        _ShineSpeed ("Shine Speed", Float) = 1.2
        _ShineFrequency ("Shine Frequency (per world unit)", Float) = 0.06
        _ShineWidth ("Shine Width", Range(0.01, 1)) = 0.18
        _ShineStrength ("Shine Strength", Range(0, 2)) = 0.9
        _TwinkleStrength ("Twinkle Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Fill;
            float _HueSpeed;
            float _HueScale;
            float _Saturation;
            float _ShineSpeed;
            float _ShineFrequency;
            float _ShineWidth;
            float _ShineStrength;
            float _TwinkleStrength;

            float3 HueToRgb(float h)
            {
                float3 rgb = saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
                return lerp(float3(1, 1, 1), rgb, _Saturation);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float t = _Time.y;

                // 위치를 따라 흐르는 무지개색
                float3 rainbow = HueToRgb(i.world.x * _HueScale + i.world.y * _HueScale * 0.5 - t * _HueSpeed);

                // 대각선으로 지나가는 반짝 빛줄기
                float band = frac((i.world.x + i.world.y) * _ShineFrequency - t * _ShineSpeed);
                float shine = saturate(1 - abs(band - 0.5) / (_ShineWidth * 0.5));
                shine = shine * shine * _ShineStrength;

                // 전체가 살짝 깜빡이는 반짝임
                float twinkle = 1 + _TwinkleStrength * sin(t * 14 + i.world.x * 0.7);

                float3 lit = saturate(rainbow * twinkle + shine);
                float3 rgb = lerp(tex.rgb, lit, _Fill) * i.color.rgb;
                float a = tex.a * i.color.a;
                return fixed4(rgb * a, a);
            }
            ENDCG
        }
    }
}
