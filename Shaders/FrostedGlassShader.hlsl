// Frosted Glass Shader for Win2D
// Creates a liquid glass effect with blur, distortion, and edge glow

// Shader properties
#define D2D_INPUT_COUNT 1
#define D2D_INPUT0_SIMPLE

#include "d2d1effecthelpers.hlsli"

// Constant buffer for shader parameters
cbuffer constants : register(b0)
{
    float blurRadius : packoffset(c0.x);      // Blur amount (0-50)
    float refraction : packoffset(c0.y);       // Distortion amount (0-0.1)
    float transparency : packoffset(c0.z);     // Glass transparency (0-1)
    float edgeGlow : packoffset(c0.w);         // Edge glow intensity (0-1)
    float4 tintColor : packoffset(c1);         // Glass tint color (RGBA)
    float2 center : packoffset(c2.x);          // Center for radial effects
    float cornerRadius : packoffset(c2.z);     // Corner softness
    float noiseAmount : packoffset(c2.w);      // Noise for texture
};

// Simple hash function for noise
float hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// Smooth noise function
float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    
    return lerp(lerp(hash(i + float2(0.0, 0.0)), 
                     hash(i + float2(1.0, 0.0)), u.x),
                lerp(hash(i + float2(0.0, 1.0)), 
                     hash(i + float2(1.0, 1.0)), u.x), u.y);
}

// Gaussian blur kernel approximation
float4 GaussianBlur(float2 uv, float radius)
{
    float4 color = float4(0, 0, 0, 0);
    float total = 0.0;
    
    // Sample in a pattern for blur effect
    [unroll]
    for (int x = -4; x <= 4; x++)
    {
        [unroll]
        for (int y = -4; y <= 4; y++)
        {
            float2 offset = float2(x, y) * radius * 0.002;
            float weight = exp(-(x * x + y * y) / (2.0 * 4.0));
            color += D2DGetInput(0, uv + offset) * weight;
            total += weight;
        }
    }
    
    return color / total;
}

D2D_PS_ENTRY(main)
{
    float2 uv = D2DGetScenePosition().xy;
    float2 inputSize = D2DGetInputSize(0);
    float2 normalizedUV = uv / inputSize;
    
    // Calculate distance from edges for rounded corners effect
    float2 fromCenter = abs(normalizedUV - 0.5) * 2.0;
    float cornerDist = length(max(fromCenter - (1.0 - cornerRadius), 0.0));
    float cornerMask = 1.0 - smoothstep(0.0, cornerRadius * 0.5, cornerDist);
    
    // Add subtle distortion for liquid glass effect
    float2 distortion = float2(
        noise(normalizedUV * 20.0 + center.x) - 0.5,
        noise(normalizedUV * 20.0 + center.y + 100.0) - 0.5
    ) * refraction;
    
    float2 distortedUV = normalizedUV + distortion;
    
    // Apply blur effect
    float4 blurredColor = GaussianBlur(distortedUV * inputSize, blurRadius);
    
    // Add noise texture for frosted appearance
    float frostedNoise = noise(normalizedUV * 100.0) * noiseAmount;
    blurredColor.rgb += frostedNoise * 0.1;
    
    // Mix with tint color
    float4 tintedColor = lerp(blurredColor, tintColor, tintColor.a * 0.3);
    
    // Calculate edge glow
    float edgeDist = min(min(normalizedUV.x, 1.0 - normalizedUV.x), 
                         min(normalizedUV.y, 1.0 - normalizedUV.y));
    float edge = 1.0 - smoothstep(0.0, 0.1, edgeDist);
    float3 glowColor = tintColor.rgb * edgeGlow * edge * 0.5;
    
    // Final color composition
    float4 finalColor;
    finalColor.rgb = tintedColor.rgb + glowColor;
    finalColor.a = transparency * cornerMask;
    
    // Add subtle specular highlight for glass appearance
    float specular = pow(max(0, dot(normalize(float3(distortion, 1.0)), float3(0.0, 0.0, 1.0))), 32.0);
    finalColor.rgb += specular * 0.1;
    
    return finalColor;
}
