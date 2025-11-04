// CollisionHighlight.fx
// Подсветка одного круга (MonoGame / MGCB friendly, DX9-style tex2D sampler)

#if OPENGL
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Текстура, в которую мы рендерим сцену (передаётся из C#)
Texture2D Texture0;
sampler2D Texture0Sampler = sampler_state { Texture = <Texture0>; };

// Параметры (в пикселях)
float2 ScreenSize   = float2(800.0, 600.0); // передаём реальные размеры окна из C#
float2 CircleCenter = float2(400.0, 300.0);
float  CircleRadius = 100.0;

// Цвет подсветки: RGB, alpha = максимальная интенсивность (0..1)
float4 HighlightColor = float4(0.0, 1.0, 0.0, 0.6);

// Флаг: показывать ли подсветку
bool ShowCollision = true;

// Вход в пиксельный шейдер: texcoord и цвет (SpriteBatch выдаёт их)
float4 PS_Main(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR
{
    // Базовый цвет сцены под этим пикселем
    float4 baseColor = tex2D(Texture0Sampler, texCoord) * color;

    if (!ShowCollision || CircleRadius <= 0.0)
        return baseColor;

    // Вычисляем позицию пикселя в экранных пикселях (texCoord ∈ [0..1])
    float2 pixelPos = texCoord * ScreenSize;

    float dist = distance(pixelPos, CircleCenter);

    if (dist <= CircleRadius)
    {
        // Плавный градиент: центр -> 1, край -> 0
        float t = saturate(1.0 - dist / CircleRadius);
        float influence = HighlightColor.a * t;

        // Смешиваем RGB; сохраняем исходную альфу
        float3 finalRgb = lerp(baseColor.rgb, HighlightColor.rgb, influence);
        return float4(finalRgb, baseColor.a);
    }

    return baseColor;
}

technique HighlightTech 
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PS_Main();
    }
};
