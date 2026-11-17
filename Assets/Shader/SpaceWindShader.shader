Shader "Custom/SpaceWindShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
    }
    SubShader
    {
        // Устанавливаем теги для корректного рендеринга прозрачности
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        Cull Off
        Lighting Off
        ZWrite Off
        // Включаем классическое смешивание цветов (Alpha Blending)
        Blend SrcAlpha OneMinusSrcAlpha

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
            float _ScrollSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Смещаем текстурные координаты по оси X в зависимости от времени
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.x += _Time.y * _ScrollSpeed;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Читаем пиксель из текстуры. Шейдер возвращает цвет со встроенной альфой (прозрачностью)
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Возвращаем исходный цвет штриха. Все пустые пиксели автоматически станут невидимыми
                return col;
            }
            ENDCG
        }
    }
}
