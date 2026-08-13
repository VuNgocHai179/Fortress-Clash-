Shader "Custom/DiagonalDownRightRepeat"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Scroll Speed", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Speed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float time = _Time.y;
                // Di chuyển từ trên trái xuống dưới phải: X tăng, Y giảm
                //float2 scroll = float2(_Speed * time, -_Speed * time);
                float2 scroll = float2(_Speed * time, 0);
                float2 uv = i.uv - scroll;

                uv = frac(uv);

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}