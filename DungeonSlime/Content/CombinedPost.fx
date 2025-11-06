#if OPENGL
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D Texture0;
sampler2D Texture0Sampler = sampler_state { Texture = <Texture0>; };

// Основные параметры
float Saturation = 0.0;
float2 ScreenSize = float2(800.0, 600.0);

// Highlight default (если нужно)
float4 DefaultHighlight = float4(0.0, 1.0, 0.0, 0.8);

// Упакованные данные: для каждого круга две записи float4
// data[i].xy = center.xy, data[i].z = radius, data[i].w = thickness
// color[i] = float4(r,g,b,a)
#define MAX_CIRCLES 48
float4 CircleData[MAX_CIRCLES];
float4 CircleColor[MAX_CIRCLES];
int    CircleCount = 0;
bool   ShowCollision = false;

// Pixel shader
float4 PS_Main(float2 texCoord : TEXCOORD0, float4 vertColor : COLOR0) : COLOR
{
    float4 baseColor = tex2D(Texture0Sampler, texCoord) * vertColor;

    // grayscale
    float lum = dot(baseColor.rgb, float3(0.3, 0.59, 0.11));
    float3 gray = float3(lum, lum, lum);
    float3 col = lerp(baseColor.rgb, gray, Saturation);

    if (ShowCollision && CircleCount > 0)
    {
        float2 pixelPos = texCoord * ScreenSize;
        float3 sumRGB = float3(0.0, 0.0, 0.0);
        float totalInf = 0.0;

        [unroll]
        for (int i = 0; i < CircleCount; i++)
        {
            float4 d = CircleData[i];
            float2 c = d.xy;
            float r = d.z;
            float thickness = d.w;
            float4 cc = CircleColor[i]; // rgba

            // compute distance to circle outline
            float dist = distance(pixelPos, c);
            float t = abs(dist - r);

            float half = max(thickness * 0.5, 0.0001);
            float n = saturate(t / half);
            float intensity = pow(1.0 - n, 1.0); // can adjust softness
            float inf = saturate(intensity * cc.a);

            if (inf > 0.0001)
            {
                sumRGB += cc.rgb * inf;
                totalInf += inf;
            }
        }

        if (totalInf > 0.0)
        {
            float finalInf = saturate(totalInf);
            float3 highlightRGB = sumRGB / max(totalInf, 1e-6);
            col = lerp(col, highlightRGB, finalInf);
        }
    }

    return float4(col, baseColor.a);
}

technique CombinedTech
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PS_Main();
    }
};
