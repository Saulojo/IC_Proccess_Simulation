#ifndef INDUSTRIAL_SIM_BOILER_WATER_SURFACE_INCLUDED
#define INDUSTRIAL_SIM_BOILER_WATER_SURFACE_INCLUDED

float EvaluateBoilerWaterHeight(
    float2 positionXZ,
    float timeValue,
    float waveAmplitude,
    float waveScale,
    float waveSpeed,
    float circulationIntensity,
    float boilingIntensity,
    out float foamMask)
{
    float safeScale = max(waveScale, 0.0001);

    float2 p =
        positionXZ *
        safeScale;

    float t =
        timeValue *
        waveSpeed;

    // Ondas suaves gerais.
    float waveA =
        sin(
            dot(
                p,
                float2(0.82, 0.57))
            * 1.65
            + t);

    float waveB =
        sin(
            dot(
                p,
                float2(-0.41, 0.91))
            * 2.20
            - t * 1.31);

    float waveC =
        sin(
            length(p) * 2.75
            - t * 0.74);

    // Movimento proveniente da circulação.
    float circulationWave =
        sin(
            p.x * 3.60
            + p.y * 1.25
            - t * 2.30);

    // Pequena irregularidade usada posteriormente
    // para representar ebulição.
    float boilingWaveA =
        sin(
            p.x * 7.30
            + t * 4.20)
        *
        sin(
            p.y * 6.10
            - t * 3.60);

    float boilingWaveB =
        sin(
            dot(
                p,
                float2(1.71, -1.23))
            * 5.40
            + t * 5.10);

    float ambientHeight =
        waveA * 0.48
        + waveB * 0.32
        + waveC * 0.20;

    float circulationHeight =
        circulationWave
        * circulationIntensity
        * 0.32;

    float boilingHeight =
        (
            boilingWaveA * 0.65
            + boilingWaveB * 0.35
        )
        * boilingIntensity
        * 0.22;

    float boilingCrest =
        saturate(
            boilingWaveA * 0.5
            + 0.5);

    foamMask =
        pow(
            boilingCrest,
            8.0)
        * boilingIntensity
        * 0.45;

    return
        (
            ambientHeight
            + circulationHeight
            + boilingHeight
        )
        * waveAmplitude;
}

void BoilerWaterSurface_float(
    float3 PositionOS,
    float TimeValue,
    float WaveAmplitude,
    float WaveScale,
    float WaveSpeed,
    float CirculationIntensity,
    float BoilingIntensity,
    out float3 OffsetOS,
    out float3 NormalOS,
    out float FoamMask)
{
    float centerFoam;

    float centerHeight =
        EvaluateBoilerWaterHeight(
            PositionOS.xz,
            TimeValue,
            WaveAmplitude,
            WaveScale,
            WaveSpeed,
            CirculationIntensity,
            BoilingIntensity,
            centerFoam);

    const float epsilon = 0.01;

    float rightFoam;

    float rightHeight =
        EvaluateBoilerWaterHeight(
            PositionOS.xz
            + float2(epsilon, 0.0),
            TimeValue,
            WaveAmplitude,
            WaveScale,
            WaveSpeed,
            CirculationIntensity,
            BoilingIntensity,
            rightFoam);

    float forwardFoam;

    float forwardHeight =
        EvaluateBoilerWaterHeight(
            PositionOS.xz
            + float2(0.0, epsilon),
            TimeValue,
            WaveAmplitude,
            WaveScale,
            WaveSpeed,
            CirculationIntensity,
            BoilingIntensity,
            forwardFoam);

    OffsetOS =
        float3(
            0.0,
            centerHeight,
            0.0);

    NormalOS =
        normalize(
            float3(
                centerHeight - rightHeight,
                epsilon,
                centerHeight - forwardHeight));

    FoamMask =
        saturate(centerFoam);
}

#endif