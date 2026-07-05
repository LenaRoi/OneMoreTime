Shader "Custom/EqualizerBars"
{
    // Tüm equalizer bloklarını TEK mesh olarak çizer AMA sahne ışığından etkilenir
    // (Standard PBR). RhythmLight'lar beyaz çubukları neon renge boyar — oyunun temel
    // mekaniği. Her vertex hangi sütun/segmentte olduğunu taşır (UV0.x=bar, UV0.y=seg);
    // script her kare _Levels[bar] = o sütunda yanan segment sayısı değerini verir.
    // Yanan segment görünür (parlak albedo), yanmayan sönük ya da tamamen kesilir (clip).
    Properties
    {
        _Brightness    ("Brightness (yanık albedo çarpanı)", Float) = 2.2
        _UnlitDim      ("Unlit Dim", Float) = 0.12
        _ShowUnlit     ("Show Unlit (0/1)", Float) = 0
        _Glossiness    ("Smoothness", Range(0,1)) = 0.2
        _Metallic      ("Metallic", Range(0,1)) = 0.0
        _EmissionBoost ("Emission (kendinden parlama, 0 = sadece ışık)", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert
        #pragma target 3.0

        float _Levels[128];     // sütun başına yanan segment sayısı (bar <= 128)
        float _Brightness;
        float _UnlitDim;
        float _ShowUnlit;
        half  _Glossiness;
        half  _Metallic;
        float _EmissionBoost;

        struct Input
        {
            float4 vcol;        // sütunun beyaz tonu (vertex color)
            float litFlag;      // 0/1 bu segment yanık mı
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            int b = (int)(v.texcoord.x + 0.5);
            float level = _Levels[b];
            o.litFlag = step(v.texcoord.y + 0.5, level);   // segment < level ise yanık
            o.vcol = v.color;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float lit = IN.litFlag;
            clip(lit + _ShowUnlit - 0.5);           // sönük & showUnlit=0 → pikseli at
            float3 baseCol = IN.vcol.rgb;
            float k = lerp(_UnlitDim, _Brightness, lit);
            o.Albedo = baseCol * k;                 // SAHNE IŞIĞI bunu boyar (neon)
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = baseCol * k * _EmissionBoost * lit;   // istenirse kendinden parlama
        }
        ENDCG
    }
    Fallback "Diffuse"
}
