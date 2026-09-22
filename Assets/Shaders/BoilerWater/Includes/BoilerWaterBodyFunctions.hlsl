// UNITY_SHADER_NO_UPGRADE

#ifndef INDUSTRIAL_SIM_BOILER_WATER_BODY_INCLUDED
#define INDUSTRIAL_SIM_BOILER_WATER_BODY_INCLUDED


// ============================================================================
// WATER BODY OPTICAL APPROXIMATION
//
// This shader does NOT attempt to implement a true participating medium.
//
// Instead it provides a lightweight approximation suitable for the
// procedural water geometry:
//
// - depth-dependent coloration
// - depth-dependent transparency
// - subtle shallow/deep transition
//
// The dedicated free-surface shader will later handle:
// - Fresnel
// - reflections
// - micro waves
// - refraction
// ============================================================================


void EvaluateBoilerWaterBodyInternal(
    float3 positionWS,
    float waterSurfaceWorldY,

    float4 shallowColor,
    float4 deepColor,

    float absorptionDistance,

    float shallowAlpha,
    float deepAlpha,

    float depthContrast,

    out float3 baseColor,
    out float alpha,
    out float depth01)
{
    // ------------------------------------------------------------------------
    // Vertical depth below the common hydraulic surface.
    // ------------------------------------------------------------------------

    float depth =
        max(
            waterSurfaceWorldY -
            positionWS.y,
            0.0);


    // ------------------------------------------------------------------------
    // Absorption approximation
    //
    // 1 - exp(-x) produces a smooth asymptotic response similar to optical
    // absorption without requiring a volumetric renderer.
    //
    // absorptionDistance represents roughly how many meters are required
    // before the water approaches its deeper coloration.
    // ------------------------------------------------------------------------

    float safeAbsorptionDistance =
        max(
            absorptionDistance,
            0.0001);


    float absorption =
        1.0 -
        exp(
            -depth /
            safeAbsorptionDistance);


    absorption =
        saturate(
            absorption);


    // ------------------------------------------------------------------------
    // Contrast shaping.
    //
    // > 1 keeps the shallow region clear for longer.
    // < 1 makes it become deep-colored more quickly.
    // ------------------------------------------------------------------------

    float safeContrast =
        max(
            depthContrast,
            0.01);


    depth01 =
        pow(
            absorption,
            safeContrast);


    // ------------------------------------------------------------------------
    // Color
    // ------------------------------------------------------------------------

    baseColor =
        lerp(
            shallowColor.rgb,
            deepColor.rgb,
            depth01);


    // ------------------------------------------------------------------------
    // Transparency
    //
    // The water body remains quite transparent close to the free surface
    // and becomes progressively denser at depth.
    // ------------------------------------------------------------------------

    float minAlpha =
        saturate(
            shallowAlpha);


    float maxAlpha =
        max(
            minAlpha,
            saturate(deepAlpha));


    alpha =
        lerp(
            minAlpha,
            maxAlpha,
            depth01);
}


// ============================================================================
// SHADER GRAPH - FLOAT
// ============================================================================

void BoilerWaterBody_float(
    float3 PositionWS,
    float WaterSurfaceWorldY,

    float4 ShallowColor,
    float4 DeepColor,

    float AbsorptionDistance,

    float ShallowAlpha,
    float DeepAlpha,

    float DepthContrast,

    out float3 BaseColor,
    out float Alpha,
    out float Depth01)
{
    EvaluateBoilerWaterBodyInternal(
        PositionWS,
        WaterSurfaceWorldY,

        ShallowColor,
        DeepColor,

        AbsorptionDistance,

        ShallowAlpha,
        DeepAlpha,

        DepthContrast,

        BaseColor,
        Alpha,
        Depth01);
}


// ============================================================================
// SHADER GRAPH - HALF
// ============================================================================

void BoilerWaterBody_half(
    half3 PositionWS,
    half WaterSurfaceWorldY,

    half4 ShallowColor,
    half4 DeepColor,

    half AbsorptionDistance,

    half ShallowAlpha,
    half DeepAlpha,

    half DepthContrast,

    out half3 BaseColor,
    out half Alpha,
    out half Depth01)
{
    float3 color;
    float alphaValue;
    float depthValue;

    EvaluateBoilerWaterBodyInternal(
        (float3)PositionWS,
        (float)WaterSurfaceWorldY,

        (float4)ShallowColor,
        (float4)DeepColor,

        (float)AbsorptionDistance,

        (float)ShallowAlpha,
        (float)DeepAlpha,

        (float)DepthContrast,

        color,
        alphaValue,
        depthValue);

    BaseColor =
        (half3)color;

    Alpha =
        (half)alphaValue;

    Depth01 =
        (half)depthValue;
}


#endif