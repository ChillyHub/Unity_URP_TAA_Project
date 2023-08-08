#ifndef CUSTOM_SAMPLE_UTILS_INCLUDED
#define CUSTOM_SAMPLE_UTILS_INCLUDED

// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

//====
//note: normalized random, float=[0;1[
float PDnrand( float2 n ) {
    return frac( sin(dot(n.xy, float2(12.9898f, 78.233f)))* 43758.5453f );
}
float2 PDnrand2( float2 n ) {
    return frac( sin(dot(n.xy, float2(12.9898f, 78.233f)))* float2(43758.5453f, 28001.8384f) );
}
float3 PDnrand3( float2 n ) {
    return frac( sin(dot(n.xy, float2(12.9898f, 78.233f)))* float3(43758.5453f, 28001.8384f, 50849.4141f ) );
}
float4 PDnrand4( float2 n ) {
    return frac( sin(dot(n.xy, float2(12.9898f, 78.233f)))* float4(43758.5453f, 28001.8384f, 50849.4141f, 12996.89f) );
}

//====
//note: signed random, float=[-1;1[
float PDsrand( float2 n ) {
    return PDnrand( n ) * 2 - 1;
}
float2 PDsrand2( float2 n ) {
    return PDnrand2( n ) * 2 - 1;
}
float3 PDsrand3( float2 n ) {
    return PDnrand3( n ) * 2 - 1;
}
float4 PDsrand4( float2 n ) {
    return PDnrand4( n ) * 2 - 1;
}

float3 TransformRGB2YCoCg(float3 c)
{
    // Y  = R/4 + G/2 + B/4
    // Co = R/2 - B/2
    // Cg = -R/4 + G/2 - B/4
    return float3(
         c.x / 4.0 + c.y / 2.0 + c.z / 4.0,
         c.x / 2.0 - c.z / 2.0,
        -c.x / 4.0 + c.y / 2.0 - c.z / 4.0
    );
}

float3 TransformYCoCg2RGB(float3 c)
{
    // R = Y + Co - Cg
    // G = Y + Cg
    // B = Y - Co - Cg
    return saturate(float3(
        c.x + c.y - c.z,
        c.x + c.z,
        c.x - c.y - c.z
    ));
}

half3 MappingColor(half3 color)
{
    #if _ENABLE_YCOCG
        // half3 ycocg = TransformRGB2YCoCg(color);
        // return ycocg * rcp(1.0 + ycocg.r);
    
        return TransformRGB2YCoCg(color * rcp(1.0 + Luminance(color)));
    
        // return color * rcp(1.0 + Luminance(color));

        // return color;
        // return TransformRGB2YCoCg(color);
    #else
        return color;
    #endif
}

half3 ResolveColor(half3 color)
{
    #if _ENABLE_YCOCG
        half3 rgb = TransformYCoCg2RGB(color);
        return rgb * rcp(1.0 - Luminance(rgb));
    
        // return TransformYCoCg2RGB(color * rcp(1.0 - color.r));

        // return color * rcp(1.0 - Luminance(color));

        // return color;
        // return TransformYCoCg2RGB(color);
    #else
        return color;
    #endif
}

#endif