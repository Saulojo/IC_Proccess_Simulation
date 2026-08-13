using UnityEngine;

namespace IndustrialSim.UnityRuntime.Visualization.Boiler
{
    /// <summary>
    /// Controlador temporário para validar visualmente a água da caldeira.
    ///
    /// Nesta etapa ele NÃO utiliza o modelo matemático.
    /// Os valores são definidos manualmente pelo Inspector.
    ///
    /// Responsabilidades:
    /// - definir o nível global da água;
    /// - atualizar os volumes dos tambores;
    /// - atualizar os volumes dos tubos;
    /// - posicionar a superfície livre;
    /// - calcular a largura da superfície no tambor cilíndrico;
    /// - controlar circulação visual;
    /// - controlar agitação/fervura visual.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BoilerWaterPreviewController : MonoBehaviour
    {
        // ================================================================
        // UPPER DRUM GEOMETRY
        // ================================================================

        [Header("Upper Drum Geometry")]

        [SerializeField]
        private Transform upperDrumCenter;

        [SerializeField]
        private Transform upperDrumAxisStart;

        [SerializeField]
        private Transform upperDrumAxisEnd;

        [Tooltip("Raio interno do tambor superior em metros.")]
        [SerializeField, Min(0.01f)]
        private float upperDrumInnerRadius = 0.52f;

        [Tooltip(
            "Margem removida de cada extremidade longitudinal " +
            "da superfície para evitar interseção com as paredes.")]
        [SerializeField, Min(0f)]
        private float surfaceEndInset = 0.025f;

        [Tooltip(
            "Margem radial para impedir que a superfície " +
            "atravesse visualmente a parede do tambor.")]
        [SerializeField, Min(0f)]
        private float surfaceRadialInset = 0.015f;

        [Tooltip(
            "Pequeno offset vertical usado para evitar z-fighting " +
            "entre o volume cortado e a superfície.")]
        [SerializeField, Min(0f)]
        private float surfaceVerticalOffset = 0.002f;


        // ================================================================
        // WATER VOLUMES
        // ================================================================

        [Header("Water Volume Renderers")]

        [Tooltip(
            "Renderers dos volumes de água dos tambores " +
            "superior e inferior.")]
        [SerializeField]
        private Renderer[] drumWaterRenderers;

        [Tooltip(
            "Renderers dos volumes internos de água dos tubos.")]
        [SerializeField]
        private Renderer[] tubeWaterRenderers;


        // ================================================================
        // FREE SURFACE
        // ================================================================

        [Header("Upper Drum Free Surface")]

        [SerializeField]
        private Transform upperDrumSurface;

        [SerializeField]
        private Renderer upperDrumSurfaceRenderer;


        // ================================================================
        // PREVIEW CONTROLS
        // ================================================================

        [Header("Visual Preview")]

        [Tooltip(
            "0 = nível próximo ao fundo do tambor superior.\n" +
            "1 = nível próximo ao topo do tambor superior.")]
        [SerializeField, Range(0.02f, 0.98f)]
        private float waterLevel01 = 0.50f;

        [Tooltip(
            "Intensidade visual da circulação natural da água.")]
        [SerializeField, Range(0f, 1f)]
        private float circulation01 = 0.25f;

        [Tooltip(
            "Intensidade visual de ebulição. " +
            "Mantenha baixa durante a primeira validação.")]
        [SerializeField, Range(0f, 1f)]
        private float boilingIntensity01 = 0f;

        [Tooltip(
            "Pequena agitação presente mesmo sem circulação.")]
        [SerializeField, Range(0f, 1f)]
        private float ambientAgitation01 = 0.15f;


        // ================================================================
        // AUTOMATIC TEST
        // ================================================================

        [Header("Automatic Level Test")]

        [SerializeField]
        private bool animateLevelInPlayMode;

        [SerializeField, Min(1f)]
        private float levelAnimationPeriod = 12f;


        // ================================================================
        // SURFACE SETTINGS
        // ================================================================

        [Header("Surface Animation")]

        [SerializeField, Min(0f)]
        private float minimumWaveAmplitude = 0.0015f;

        [SerializeField, Min(0f)]
        private float maximumWaveAmplitude = 0.010f;

        [SerializeField, Min(0f)]
        private float minimumWaveSpeed = 0.25f;

        [SerializeField, Min(0f)]
        private float maximumWaveSpeed = 1.20f;


        // ================================================================
        // INTERNAL
        // ================================================================

        private MaterialPropertyBlock propertyBlock;

        private bool refreshRequested = true;


        // ================================================================
        // SHADER PROPERTY IDS
        // ================================================================

        private static readonly int LiquidLevelWorldYId =
            Shader.PropertyToID("_LiquidLevelWorldY");

        private static readonly int FlowIntensityId =
            Shader.PropertyToID("_FlowIntensity");

        private static readonly int FlowSpeedId =
            Shader.PropertyToID("_FlowSpeed");

        private static readonly int WaveAmplitudeId =
            Shader.PropertyToID("_WaveAmplitude");

        private static readonly int WaveSpeedId =
            Shader.PropertyToID("_WaveSpeed");

        private static readonly int CirculationIntensityId =
            Shader.PropertyToID("_CirculationIntensity");

        private static readonly int BoilingIntensityId =
            Shader.PropertyToID("_BoilingIntensity");


        // ================================================================
        // PUBLIC READ-ONLY VALUES
        // ================================================================

        public float WaterLevel01 => waterLevel01;

        public float CurrentWaterLevelWorldY
        {
            get
            {
                if (upperDrumCenter == null)
                    return 0f;

                float bottomY =
                    upperDrumCenter.position.y
                    - upperDrumInnerRadius;

                float topY =
                    upperDrumCenter.position.y
                    + upperDrumInnerRadius;

                return Mathf.Lerp(
                    bottomY,
                    topY,
                    waterLevel01);
            }
        }


        // ================================================================
        // UNITY EVENTS
        // ================================================================

        private void OnEnable()
        {
            EnsurePropertyBlock();

            refreshRequested = true;
        }

        private void OnValidate()
        {
            waterLevel01 =
                Mathf.Clamp(
                    waterLevel01,
                    0.02f,
                    0.98f);

            circulation01 =
                Mathf.Clamp01(circulation01);

            boilingIntensity01 =
                Mathf.Clamp01(boilingIntensity01);

            ambientAgitation01 =
                Mathf.Clamp01(ambientAgitation01);

            upperDrumInnerRadius =
                Mathf.Max(
                    0.01f,
                    upperDrumInnerRadius);

            surfaceEndInset =
                Mathf.Max(
                    0f,
                    surfaceEndInset);

            surfaceRadialInset =
                Mathf.Max(
                    0f,
                    surfaceRadialInset);

            surfaceVerticalOffset =
                Mathf.Max(
                    0f,
                    surfaceVerticalOffset);

            maximumWaveAmplitude =
                Mathf.Max(
                    minimumWaveAmplitude,
                    maximumWaveAmplitude);

            maximumWaveSpeed =
                Mathf.Max(
                    minimumWaveSpeed,
                    maximumWaveSpeed);

            levelAnimationPeriod =
                Mathf.Max(
                    1f,
                    levelAnimationPeriod);

            refreshRequested = true;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (animateLevelInPlayMode)
                {
                    UpdateAutomaticLevel();
                }

                ApplyPreview();

                return;
            }

            // Em Edit Mode só atualizamos quando houve alguma alteração.
            if (refreshRequested)
            {
                ApplyPreview();

                refreshRequested = false;
            }
        }


        // ================================================================
        // PUBLIC API
        // ================================================================

        /// <summary>
        /// Será substituído posteriormente pelo estado calculado
        /// pelo modelo matemático da caldeira.
        /// </summary>
        public void SetPreviewState(
            float normalizedLevel,
            float normalizedCirculation,
            float normalizedBoilingIntensity)
        {
            waterLevel01 =
                Mathf.Clamp(
                    normalizedLevel,
                    0.02f,
                    0.98f);

            circulation01 =
                Mathf.Clamp01(
                    normalizedCirculation);

            boilingIntensity01 =
                Mathf.Clamp01(
                    normalizedBoilingIntensity);

            ApplyPreview();
        }

        [ContextMenu("Apply Preview Now")]
        public void ApplyPreview()
        {
            if (!ValidateRequiredReferences())
                return;

            EnsurePropertyBlock();

            float liquidLevelWorldY =
                CurrentWaterLevelWorldY;

            UpdateDrumWater(
                liquidLevelWorldY);

            UpdateTubeWater(
                liquidLevelWorldY);

            UpdateFreeSurface(
                liquidLevelWorldY);
        }


        // ================================================================
        // AUTOMATIC TEST
        // ================================================================

        private void UpdateAutomaticLevel()
        {
            float phase =
                Time.time
                * Mathf.PI
                * 2f
                / levelAnimationPeriod;

            waterLevel01 =
                0.50f
                + Mathf.Sin(phase)
                * 0.45f;
        }


        // ================================================================
        // DRUM WATER
        // ================================================================

        private void UpdateDrumWater(
            float liquidLevelWorldY)
        {
            float drumFlowIntensity =
                circulation01 * 0.15f;

            float drumFlowSpeed =
                Mathf.Lerp(
                    0.10f,
                    0.40f,
                    circulation01);

            UpdateVolumeRenderers(
                drumWaterRenderers,
                liquidLevelWorldY,
                drumFlowIntensity,
                drumFlowSpeed);
        }


        // ================================================================
        // TUBE WATER
        // ================================================================

        private void UpdateTubeWater(
            float liquidLevelWorldY)
        {
            float tubeFlowIntensity =
                circulation01;

            float tubeFlowSpeed =
                Mathf.Lerp(
                    0.25f,
                    1.60f,
                    circulation01);

            UpdateVolumeRenderers(
                tubeWaterRenderers,
                liquidLevelWorldY,
                tubeFlowIntensity,
                tubeFlowSpeed);
        }


        // ================================================================
        // GENERIC VOLUME UPDATE
        // ================================================================

        private void UpdateVolumeRenderers(
            Renderer[] renderers,
            float liquidLevelWorldY,
            float flowIntensity,
            float flowSpeed)
        {
            if (renderers == null)
                return;

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(
                    propertyBlock);

                propertyBlock.SetFloat(
                    LiquidLevelWorldYId,
                    liquidLevelWorldY);

                propertyBlock.SetFloat(
                    FlowIntensityId,
                    flowIntensity);

                propertyBlock.SetFloat(
                    FlowSpeedId,
                    flowSpeed);

                targetRenderer.SetPropertyBlock(
                    propertyBlock);

                propertyBlock.Clear();
            }
        }


        // ================================================================
        // FREE SURFACE
        // ================================================================

        private void UpdateFreeSurface(
            float liquidLevelWorldY)
        {
            Vector3 axisVector =
                upperDrumAxisEnd.position
                - upperDrumAxisStart.position;

            float axisLength =
                axisVector.magnitude;

            if (axisLength <= 0.001f)
                return;

            Vector3 horizontalAxis =
                Vector3.ProjectOnPlane(
                    axisVector,
                    Vector3.up);

            if (horizontalAxis.sqrMagnitude <= 0.000001f)
            {
                Debug.LogWarning(
                    "Upper drum AxisStart/AxisEnd must define " +
                    "a horizontal longitudinal axis.",
                    this);

                return;
            }

            Vector3 lengthDirection =
                horizontalAxis.normalized;

            float internalLength =
                axisLength
                - surfaceEndInset * 2f;

            internalLength =
                Mathf.Max(
                    0.001f,
                    internalLength);


            // ------------------------------------------------------------
            // CALCULATE CHORD WIDTH
            // ------------------------------------------------------------

            float levelOffsetFromCenter =
                liquidLevelWorldY
                - upperDrumCenter.position.y;

            float radiusSquared =
                upperDrumInnerRadius
                * upperDrumInnerRadius;

            float offsetSquared =
                levelOffsetFromCenter
                * levelOffsetFromCenter;

            float halfChordSquared =
                Mathf.Max(
                    0f,
                    radiusSquared
                    - offsetSquared);

            float halfChord =
                Mathf.Sqrt(
                    halfChordSquared);

            float surfaceWidth =
                halfChord * 2f
                - surfaceRadialInset * 2f;

            surfaceWidth =
                Mathf.Max(
                    0.001f,
                    surfaceWidth);


            // ------------------------------------------------------------
            // POSITION
            // ------------------------------------------------------------

            Vector3 surfacePosition =
                (
                    upperDrumAxisStart.position
                    + upperDrumAxisEnd.position
                )
                * 0.5f;

            surfacePosition.y =
                liquidLevelWorldY
                + surfaceVerticalOffset;


            // ------------------------------------------------------------
            // ROTATION
            // ------------------------------------------------------------

            Vector3 surfaceForward =
                Vector3.Cross(
                    lengthDirection,
                    Vector3.up)
                .normalized;

            Quaternion surfaceRotation =
                Quaternion.LookRotation(
                    surfaceForward,
                    Vector3.up);


            // ------------------------------------------------------------
            // APPLY TRANSFORM
            // ------------------------------------------------------------

            upperDrumSurface.SetPositionAndRotation(
                surfacePosition,
                surfaceRotation);

            upperDrumSurface.localScale =
                new Vector3(
                    internalLength,
                    1f,
                    surfaceWidth);


            // ------------------------------------------------------------
            // SURFACE SHADER
            // ------------------------------------------------------------

            float combinedAgitation =
                Mathf.Clamp01(
                    ambientAgitation01
                    + circulation01 * 0.40f
                    + boilingIntensity01 * 0.70f);

            float waveAmplitude =
                Mathf.Lerp(
                    minimumWaveAmplitude,
                    maximumWaveAmplitude,
                    combinedAgitation);

            float waveSpeed =
                Mathf.Lerp(
                    minimumWaveSpeed,
                    maximumWaveSpeed,
                    combinedAgitation);

            upperDrumSurfaceRenderer.GetPropertyBlock(
                propertyBlock);

            propertyBlock.SetFloat(
                WaveAmplitudeId,
                waveAmplitude);

            propertyBlock.SetFloat(
                WaveSpeedId,
                waveSpeed);

            propertyBlock.SetFloat(
                CirculationIntensityId,
                circulation01);

            propertyBlock.SetFloat(
                BoilingIntensityId,
                boilingIntensity01);

            upperDrumSurfaceRenderer.SetPropertyBlock(
                propertyBlock);

            propertyBlock.Clear();
        }


        // ================================================================
        // HELPERS
        // ================================================================

        private void EnsurePropertyBlock()
        {
            propertyBlock ??=
                new MaterialPropertyBlock();
        }

        private bool ValidateRequiredReferences()
        {
            return
                upperDrumCenter != null
                && upperDrumAxisStart != null
                && upperDrumAxisEnd != null
                && upperDrumSurface != null
                && upperDrumSurfaceRenderer != null;
        }


        // ================================================================
        // EDITOR GIZMOS
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            if (upperDrumCenter == null)
                return;

            Gizmos.DrawWireSphere(
                upperDrumCenter.position,
                upperDrumInnerRadius);

            if (upperDrumAxisStart != null
                && upperDrumAxisEnd != null)
            {
                Gizmos.DrawLine(
                    upperDrumAxisStart.position,
                    upperDrumAxisEnd.position);
            }

            Vector3 levelMarker =
                upperDrumCenter.position;

            levelMarker.y =
                CurrentWaterLevelWorldY;

            Gizmos.DrawWireSphere(
                levelMarker,
                0.035f);
        }
    }
}