#if OPENGL
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D Texture0;
sampler2D Texture0Sampler = sampler_state { Texture = <Texture0>; };


float Saturation = 0.0;
float2 ScreenSize = float2(800.0, 600.0);

float4 HighlightColor = float4(0.0, 1.0, 0.0, 0.8);

float OutlineThickness = 4.0;
float OutlineSoftness = 1.0;

#define MAX_CIRCLES 64
float2 CircleCenters[MAX_CIRCLES];
float  CircleRadii[MAX_CIRCLES];
int    CircleCount = 0;

bool ShowCollision = false;

float4 PS_Main(float2 texCoord : TEXCOORD0, float4 vertColor : COLOR0) : COLOR
{

    float4 baseColor = tex2D(Texture0Sampler, texCoord) * vertColor;


    float lum = dot(baseColor.rgb, float3(0.3, 0.59, 0.11));
    float3 gray = float3(lum, lum, lum);
    float3 col = lerp(baseColor.rgb, gray, Saturation);

    if (ShowCollision && CircleCount > 0)
    {
        float2 pixelPos = texCoord * ScreenSize;
        float3 highlight = col;
        float totalInfluence = 0.0;


        [unroll]
        for (int i = 0; i < CircleCount; i++)
        {
            float2 c = CircleCenters[i];
            float  r = CircleRadii[i];

            float d = distance(pixelPos, c);
            float t = abs(d - r);

            float half = max(OutlineThickness * 0.5, 0.0001);
            float n = saturate(t / half); 
            float intensity = pow(1.0 - n, max(0.0001, OutlineSoftness)); // 1..0
            float inf = intensity * HighlightColor.a;


            totalInfluence = max(totalInfluence, inf);
        }

        if (totalInfluence > 0.0)
        {
            col = lerp(col, HighlightColor.rgb, totalInfluence);
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
