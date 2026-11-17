Shader "Custom/SpriteOutlineWithBlink"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineSize ("Outline Size", Range(0, 10)) = 1
        
        // Новые параметры для настройки мерцания
        [Toggle] _EnableBlink ("Enable Blink", Float) = 1
        _BlinkSpeed ("Blink Speed", Range(0, 20)) = 3
        _MinAlpha ("Min Glow Intensity", Range(0, 1)) = 0.2
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
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };
            
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineSize;
            float4 _MainTex_TexelSize;

            // Переменные для мерцания
            float _EnableBlink;
            float _BlinkSpeed;
            float _MinAlpha;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // 1. Поиск внешних границ
                float2 size = _MainTex_TexelSize.xy * _OutlineSize;
                
                float maxAlpha = 0.0;
                maxAlpha = max(maxAlpha, tex2D(_MainTex, IN.texcoord + float2(size.x, 0)).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, IN.texcoord - float2(size.x, 0)).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, IN.texcoord + float2(0, size.y)).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, IN.texcoord - float2(0, size.y)).a);

                // 2. Логика мерцания
                fixed4 outline = _OutlineColor;
                
                if (_EnableBlink > 0.5)
                {
                    // Синусоида от времени изменяет значение от 0 до 1
                    float pulse = (sin(_Time.y * _BlinkSpeed) + 1.0) * 0.5;
                    
                    // Ограничиваем минимальную яркость, чтобы контур не исчезал полностью
                    pulse = lerp(_MinAlpha, 1.0, pulse);
                    
                    // Применяем пульсацию к прозрачности контура
                    outline.a *= pulse;
                }

                outline.rgb *= outline.a; // Учитываем альфа-блендинг Unity

                // 3. Компоновка пикселя
                fixed4 finalColor = lerp(outline * sign(maxAlpha), mainColor, mainColor.a);

                return finalColor;
            }
            ENDCG
        }
    }
}
