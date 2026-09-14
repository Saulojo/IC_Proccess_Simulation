// UNITY_SHADER_NO_UPGRADE

#ifndef INDUSTRIAL_SIM_WATER_SURFACE_INCLUDED
#define INDUSTRIAL_SIM_WATER_SURFACE_INCLUDED


// ============================================================================
// WATER V2 - PROCEDURAL FREE SURFACE
//
// Features:
// - multi-scale procedural micro waves
// - hydraulic agitation
// - localized FeedWater inlet disturbance
// - stable Fresnel
// - no vertex displacement
// ============================================================================


// ============================================================================
// LOCALIZED INLET MASK
//
// The disturbance is evaluated on the horizontal XZ plane because this is
// the free water surface.
//
// Only the Upper Drum renderer will have this effect enabled.
// ============================================================================

float EvaluateInletDisturbanceMask(
    float3 positionWS,
    float3 inletWorldPosition,
    float inletDisturbanceEnabled,
    float inletDisturbanceRadius,
    float inletDisturbanceFalloff)
{
    float enabled =
        saturate(
            inletDisturbanceEnabled);


    float safeRadius =
        max(
            inletDisturbanceRadius,
            0.001);


    float2 delta =
        positionWS.xz -
        inletWorldPosition.xz;


    float distanceFromInlet =
        length(
            delta);


    float radial01 =
        saturate(
            1.0 -
            distanceFromInlet /
            safeRadius);


    // Smooth boundary so the localized disturbance never creates an
    // obvious circular discontinuity.
    float smoothMask =
        smoothstep(
            0.0,
            1.0,
            radial01);


    float falloff =
        max(
            inletDisturbanceFalloff,
            0.05);


    smoothMask =
        pow(
            smoothMask,
            falloff);


    return
        smoothMask *
        enabled;
}


// ============================================================================
// PROCEDURAL WAVE BANK
//
// globalAgitation:
//     controls global temporal evolution.
//
// localAgitation:
//     controls local detail/activity.
//
// Keeping these separate prevents different regions of the same water surface
// from accumulating different time phases.
// ============================================================================

float2 EvaluateWaterMicroSlope(
    float2 positionXZ,
    float timeValue,

    float globalAgitation01,
    float localAgitation01,

    float waveScale,
    float waveSpeed)
{
    float globalAgitation =
        saturate(
            globalAgitation01);


    float localAgitation =
        saturate(
            localAgitation01);


    float scale =
        max(
            waveScale,
            0.0001);


    float speed =
        max(
            waveSpeed,
            0.0);


    float globalCurve =
        smoothstep(
            0.0,
            1.0,
            globalAgitation);


    float detailActivity =
        lerp(
            0.12,
            1.0,
            sqrt(
                localAgitation));


    // Important:
    // speed remains GLOBAL.
    //
    // If speed depended on localAgitation, nearby fragments could accumulate
    // different temporal phases and create a visible transition ring.
    float effectiveSpeed =
        speed *
        lerp(
            0.60,
            1.50,
            globalCurve);


    float t =
        timeValue *
        effectiveSpeed;


    float2 p =
        positionXZ *
        scale;


    // Slow spatial drift.
    float2 drift =
        float2(
            sin(t * 0.093),
            cos(t * 0.071))
        *
        0.12;


    p +=
        drift;


    // ------------------------------------------------------------------------
    // Directions
    // ------------------------------------------------------------------------

    const float2 dir1 =
        float2(
            0.963518,
            0.267644);


    const float2 dir2 =
        float2(
            -0.521450,
            0.853282);


    const float2 dir3 =
        float2(
            0.346635,
            -0.937999);


    const float2 dir4 =
        float2(
            0.788011,
            0.615661);


    const float2 dir5 =
        float2(
            -0.878817,
            -0.477159);


    const float2 dir6 =
        float2(
            0.173648,
            0.984808);


    // ------------------------------------------------------------------------
    // Phases
    // ------------------------------------------------------------------------

    float phase1 =
        dot(
            p * 0.62,
            dir1)
        +
        t * 0.71;


    float phase2 =
        dot(
            p * 0.91,
            dir2)
        -
        t * 0.89;


    float phase3 =
        dot(
            p * 1.31,
            dir3)
        +
        t * 1.13;


    float phase4 =
        dot(
            p * 1.87,
            dir4)
        -
        t * 1.47;


    float phase5 =
        dot(
            p * 2.71,
            dir5)
        +
        t * 1.91;


    float phase6 =
        dot(
            p * 3.83,
            dir6)
        -
        t * 2.27;


    // ------------------------------------------------------------------------
    // Coarse waves
    // ------------------------------------------------------------------------

    float2 coarseSlope =
        dir1 *
        cos(phase1) *
        0.42;


    coarseSlope +=
        dir2 *
        cos(phase2) *
        0.31;


    coarseSlope +=
        dir3 *
        cos(phase3) *
        0.19;


    // ------------------------------------------------------------------------
    // Fine detail
    // ------------------------------------------------------------------------

    float2 detailSlope =
        dir4 *
        cos(phase4) *
        0.12;


    detailSlope +=
        dir5 *
        cos(phase5) *
        0.075;


    detailSlope +=
        dir6 *
        cos(phase6) *
        0.045;


    float modulation =
        0.88 +
        0.12 *
        sin(
            phase1 * 0.37 +
            phase3 * 0.23);


    detailSlope *=
        modulation;


    float2 result =
        coarseSlope +
        detailSlope *
        detailActivity;


    result *=
        0.82;


    return result;
}


// ============================================================================
// MAIN SURFACE EVALUATION
// ============================================================================

void EvaluateWaterSurfaceInternal(
    float3 positionWS,
    float3 viewDirectionWS,
    float timeValue,

    float agitation01,

    float inletDisturbanceEnabled,
    float3 inletWorldPosition,
    float inletDisturbanceRadius,
    float farFieldAgitationFactor,
    float inletAgitationGain,
    float inletDisturbanceFalloff,

    float waveScale,
    float waveSpeed,
    float minimumNormalStrength,
    float maximumNormalStrength,

    float4 waterTint,
    float4 edgeTint,

    float centerAlpha,
    float edgeAlpha,
    float fresnelPower,

    out float3 normalWS,
    out float3 baseColor,
    out float alpha,
    out float fresnel)
{
    float globalAgitation =
        saturate(
            agitation01);


    // ========================================================================
    // LOCALIZED FEED-WATER RESPONSE
    // ========================================================================

    float inletMask =
        EvaluateInletDisturbanceMask(
            positionWS,
            inletWorldPosition,
            inletDisturbanceEnabled,
            inletDisturbanceRadius,
            inletDisturbanceFalloff);


    float farFieldFactor =
        saturate(
            farFieldAgitationFactor);


    float inletGain =
        max(
            farFieldFactor,
            inletAgitationGain);


    // Far away:
    //     agitation * FarFieldAgitationFactor
    //
    // Near inlet:
    //     agitation * InletAgitationGain
    float hydraulicResponse =
        lerp(
            farFieldFactor,
            inletGain,
            inletMask);


    float localAgitation =
        saturate(
            globalAgitation *
            hydraulicResponse);


    // ========================================================================
    // NORMAL STRENGTH
    // ========================================================================

    float minStrength =
        max(
            minimumNormalStrength,
            0.0);


    float maxStrength =
        max(
            minStrength,
            maximumNormalStrength);


    float localAgitationCurve =
        smoothstep(
            0.0,
            1.0,
            localAgitation);


    float normalStrength =
        lerp(
            minStrength,
            maxStrength,
            localAgitationCurve);


    // ========================================================================
    // MICRO-WAVE SLOPE
    // ========================================================================

    float2 slope =
        EvaluateWaterMicroSlope(
            positionWS.xz,
            timeValue,

            globalAgitation,
            localAgitation,

            waveScale,
            waveSpeed);


    slope *=
        normalStrength;


    normalWS =
        normalize(
            float3(
                -slope.x,
                1.0,
                -slope.y));


    // ========================================================================
    // STABLE DOUBLE-SIDED FRESNEL
    // ========================================================================

    float3 stableFresnelNormal =
        normalize(
            lerp(
                float3(
                    0.0,
                    1.0,
                    0.0),

                normalWS,

                0.30));


    float3 viewDir =
        normalize(
            viewDirectionWS);


    float NdotV =
        saturate(
            abs(
                dot(
                    stableFresnelNormal,
                    viewDir)));


    fresnel =
        pow(
            1.0 - NdotV,
            max(
                fresnelPower,
                0.01));


    // ========================================================================
    // COLOR
    // ========================================================================

    baseColor =
        lerp(
            waterTint.rgb,
            edgeTint.rgb,
            fresnel);


    // ========================================================================
    // ALPHA
    // ========================================================================

    float minAlpha =
        saturate(
            centerAlpha);


    float maxAlpha =
        max(
            minAlpha,
            saturate(
                edgeAlpha));


    alpha =
        lerp(
            minAlpha,
            maxAlpha,
            fresnel);
}


// ============================================================================
// FLOAT
// ============================================================================

void BoilerWaterSurface_float(
    float3 PositionWS,
    float3 ViewDirectionWS,
    float TimeValue,

    float Agitation01,

    float InletDisturbanceEnabled,
    float3 InletWorldPosition,
    float InletDisturbanceRadius,
    float FarFieldAgitationFactor,
    float InletAgitationGain,
    float InletDisturbanceFalloff,

    float WaveScale,
    float WaveSpeed,
    float MinimumNormalStrength,
    float MaximumNormalStrength,

    float4 WaterTint,
    float4 EdgeTint,

    float CenterAlpha,
    float EdgeAlpha,
    float FresnelPower,

    out float3 NormalWS,
    out float3 BaseColor,
    out float Alpha,
    out float Fresnel)
{
    EvaluateWaterSurfaceInternal(
        PositionWS,
        ViewDirectionWS,
        TimeValue,

        Agitation01,

        InletDisturbanceEnabled,
        InletWorldPosition,
        InletDisturbanceRadius,
        FarFieldAgitationFactor,
        InletAgitationGain,
        InletDisturbanceFalloff,

        WaveScale,
        WaveSpeed,
        MinimumNormalStrength,
        MaximumNormalStrength,

        WaterTint,
        EdgeTint,

        CenterAlpha,
        EdgeAlpha,
        FresnelPower,

        NormalWS,
        BaseColor,
        Alpha,
        Fresnel);
}


// ============================================================================
// HALF
// ============================================================================

void BoilerWaterSurface_half(
    half3 PositionWS,
    half3 ViewDirectionWS,
    half TimeValue,

    half Agitation01,

    half InletDisturbanceEnabled,
    half3 InletWorldPosition,
    half InletDisturbanceRadius,
    half FarFieldAgitationFactor,
    half InletAgitationGain,
    half InletDisturbanceFalloff,

    half WaveScale,
    half WaveSpeed,
    half MinimumNormalStrength,
    half MaximumNormalStrength,

    half4 WaterTint,
    half4 EdgeTint,

    half CenterAlpha,
    half EdgeAlpha,
    half FresnelPower,

    out half3 NormalWS,
    out half3 BaseColor,
    out half Alpha,
    out half Fresnel)
{
    float3 normal;
    float3 color;
    float alphaValue;
    float fresnelValue;


    EvaluateWaterSurfaceInternal(
        (float3)PositionWS,
        (float3)ViewDirectionWS,
        (float)TimeValue,

        (float)Agitation01,

        (float)InletDisturbanceEnabled,
        (float3)InletWorldPosition,
        (float)InletDisturbanceRadius,
        (float)FarFieldAgitationFactor,
        (float)InletAgitationGain,
        (float)InletDisturbanceFalloff,

        (float)WaveScale,
        (float)WaveSpeed,
        (float)MinimumNormalStrength,
        (float)MaximumNormalStrength,

        (float4)WaterTint,
        (float4)EdgeTint,

        (float)CenterAlpha,
        (float)EdgeAlpha,
        (float)FresnelPower,

        normal,
        color,
        alphaValue,
        fresnelValue);


    NormalWS =
        (half3)normal;


    BaseColor =
        (half3)color;


    Alpha =
        (half)alphaValue;


    Fresnel =
        (half)fresnelValue;
}


#endif