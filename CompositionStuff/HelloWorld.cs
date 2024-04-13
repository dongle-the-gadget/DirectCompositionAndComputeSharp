﻿using ComputeSharp;
using ComputeSharp.D2D1;

namespace CompositionStuff;

/// <summary>
/// A simple shader to get started with based on shadertoy new shader template.
/// Ported from <see href="https://www.shadertoy.com/new"/>.
/// </summary>
/// <param name="time">The current time since the start of the application.</param>
/// <param name="dispatchSize">The dispatch size for the current output.</param>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
internal readonly partial struct HelloWorld(float time, int2 dispatchSize) : ID2D1PixelShader
{
    /// <inheritdoc/>
    public float4 Execute()
    {
        int2 xy = (int2)D2D.GetScenePosition().XY;

        // Normalized screen space UV coordinates from 0.0 to 1.0
        float2 uv = xy / (float2)dispatchSize;

        // Time varying pixel color
        float3 col = 0.5f + (0.5f * Hlsl.Cos(time + new float3(uv, uv.X) + new float3(0, 2, 4)));

        // Output to screen
        return new(col, 1f);
    }
}