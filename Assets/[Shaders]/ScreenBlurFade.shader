Shader "Hidden/ScreenBlurFade"
{
    // Ayrılabilir (separable) Gaussian blur — Built-in RP image effect.
    // Pass 0: yatay, Pass 1: dikey. Script birkaç kez tekrarlayarak gücü artırır.
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _BlurSize;

        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
        v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

        // 9 örnekli gaussian (ağırlık toplamı = 1)
        fixed4 gblur(float2 uv, float2 dir)
        {
            float2 o = dir * _MainTex_TexelSize.xy * _BlurSize;
            fixed4 c = 0;
            c += tex2D(_MainTex, uv - o * 4) * 0.05;
            c += tex2D(_MainTex, uv - o * 3) * 0.09;
            c += tex2D(_MainTex, uv - o * 2) * 0.12;
            c += tex2D(_MainTex, uv - o * 1) * 0.15;
            c += tex2D(_MainTex, uv)         * 0.18;
            c += tex2D(_MainTex, uv + o * 1) * 0.15;
            c += tex2D(_MainTex, uv + o * 2) * 0.12;
            c += tex2D(_MainTex, uv + o * 3) * 0.09;
            c += tex2D(_MainTex, uv + o * 4) * 0.05;
            return c;
        }
        ENDCG

        Pass // 0 - yatay
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return gblur(i.uv, float2(1, 0)); }
            ENDCG
        }

        Pass // 1 - dikey
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return gblur(i.uv, float2(0, 1)); }
            ENDCG
        }
    }
    Fallback Off
}
