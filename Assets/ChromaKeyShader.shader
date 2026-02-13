Shader "Custom/ChromaKeyShader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ChromaKeyColor ("Chroma Key Color", Color) = (0.0, 0.6039, 0.2392, 1) // RGB(0, 154, 61)

        _Threshold ("Threshold", Range(0.0, 1.0)) = 0.1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            sampler2D _MainTex;
            float4 _ChromaKeyColor;
            float _Threshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texcol = tex2D(_MainTex, i.uv);
                float diff = distance(texcol.rgb, _ChromaKeyColor.rgb);
                if (diff < _Threshold)
                {
                    texcol.a = 0; // Убираем фон
                }
                return texcol;
            }
            ENDCG
        }
    }
}
