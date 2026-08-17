Texture2D TriangleTexture : register(t0);
SamplerState TriangleSampler : register(s0);

struct VertexInput
{
    float2 ScreenPosition : POSITION;
    float4 Color : COLOR;
    float2 Uv : TEXCOORD0;
    float Rhw : TEXCOORD1;
    float UseTexture : TEXCOORD2;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 Uv : TEXCOORD0;
    float UseTexture : TEXCOORD1;
};

PixelInput VSMain(VertexInput input)
{
    PixelInput output;
    float safeRhw = max(input.Rhw, 0.000001f);
    float w = 1.0f / safeRhw;
    output.Position = float4(input.ScreenPosition * w, 0.5f * w, w);
    output.Color = input.Color;
    output.Uv = input.Uv;
    output.UseTexture = input.UseTexture;
    return output;
}

float4 PSMain(PixelInput input) : SV_TARGET
{
    float4 sampled = TriangleTexture.Sample(TriangleSampler, input.Uv);
    return lerp(input.Color, sampled * input.Color, saturate(input.UseTexture));
}
