using UnityEngine;

namespace IndustrialSim.UnityRuntime.Visualization.BoilerWater
{
    /// <summary>
    /// Controlador de alto nível da representação visual de água
    /// no sistema de vasos comunicantes da caldeira.
    ///
    /// A geometria utiliza uma única superfície hidrostática global
    /// em coordenadas World Y.
    ///
    /// Nesta etapa:
    /// - controla Upper Drum;
    /// - controla Lower Drum.
    ///
    /// A próxima etapa adicionará os tubos ao mesmo nível global.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BoilerWaterVisualController : MonoBehaviour
    {
        public enum FillMappingMode
        {
            /// <summary>
            /// 0 = ponto mais baixo de todo o sistema.
            /// 1 = ponto mais alto de todo o sistema.
            ///
            /// Adequado ao modelo genérico atual de tanque.
            /// </summary>
            WholeSystemNormalized = 0,

            /// <summary>
            /// O Lower Drum e os tubos são considerados cheios,
            /// enquanto 0..1 representa somente o nível operacional
            /// dentro do Upper Drum.
            ///
            /// Será útil quando tivermos um modelo específico de caldeira.
            /// </summary>
            UpperDrumOperational = 1
        }


        // ================================================================
        // REFERENCES
        // ================================================================

        [Header("Water Geometry")]

        [SerializeField]
        private BoilerWaterMeshGenerator upperDrumWater;

        [SerializeField]
        private BoilerWaterMeshGenerator lowerDrumWater;

        [SerializeField]
        private BoilerTubeBankWaterMeshGenerator tubeBankWater;

        [Header("Feed Water Surface Disturbance")]

        [SerializeField]
        private Transform feedWaterInlet;

        [SerializeField, Min(0.01f)]
        private float inletDisturbanceRadius = 0.45f;

        [SerializeField, Range(0f, 1f)]
        private float farFieldAgitationFactor = 0.25f;

        [SerializeField, Min(0f)]
        private float inletAgitationGain = 1.25f;

        [SerializeField, Min(0.05f)]
        private float inletDisturbanceFalloff = 2.0f;


        // ================================================================
        // FILL MAPPING
        // ================================================================

        [Header("Fill Mapping")]

        [SerializeField]
        private FillMappingMode mappingMode =
            FillMappingMode.WholeSystemNormalized;

        [Tooltip(
            "Quantidade normalizada de água visual no sistema. " +
            "No modo WholeSystemNormalized: 0 = vazio, 1 = cheio.")]
        [SerializeField, Range(0f, 1f)]
        private float systemFill01 = 0.50f;

        [Tooltip(
            "Pequena tolerância utilizada para considerar um vaso vazio.")]
        [SerializeField, Min(0f)]
        private float emptyThreshold = 0.0005f;


        // ================================================================
        // PROCESS VISUAL STATE
        // ================================================================

        [Header("Process Visual State")]

        [SerializeField, Range(0f, 1f)]
        private float surfaceAgitation01 = 0f;

        [SerializeField, Range(0f, 1f)]
        private float internalFlow01 = 0f;

        [SerializeField, Range(0f, 1f)]
        private float boilingIntensity01 = 0f;


        // ================================================================
        // MONITORING
        // ================================================================

        [Header("Runtime Monitoring - Read Only")]

        [SerializeField]
        private float globalSurfaceWorldY;

        [SerializeField, Range(0f, 1f)]
        private float upperDrumLevel01;

        [SerializeField, Range(0f, 1f)]
        private float lowerDrumLevel01;


        // ================================================================
        // INTERNAL
        // ================================================================

        private bool refreshRequested = true;
        private MaterialPropertyBlock bodyPropertyBlock;
        private MaterialPropertyBlock surfacePropertyBlock;

        private static readonly int WaterSurfaceWorldYId =
            Shader.PropertyToID("_WaterSurfaceWorldY");

        private static readonly int SurfaceAgitation01Id =
            Shader.PropertyToID("_SurfaceAgitation01");

        private static readonly int InternalFlow01Id =
            Shader.PropertyToID("_InternalFlow01");

        private static readonly int BoilingIntensity01Id =
            Shader.PropertyToID("_BoilingIntensity01");

        private static readonly int InletDisturbanceEnabledId =
            Shader.PropertyToID("_InletDisturbanceEnabled");

        private static readonly int InletWorldPositionId =
            Shader.PropertyToID("_InletWorldPosition");

        private static readonly int InletDisturbanceRadiusId =
            Shader.PropertyToID("_InletDisturbanceRadius");

        private static readonly int FarFieldAgitationFactorId =
            Shader.PropertyToID("_FarFieldAgitationFactor");

        private static readonly int InletAgitationGainId =
            Shader.PropertyToID("_InletAgitationGain");

        private static readonly int InletDisturbanceFalloffId =
            Shader.PropertyToID("_InletDisturbanceFalloff");


        // ================================================================
        // PUBLIC API
        // ================================================================

        public float SystemFill01 =>
            systemFill01;

        public float GlobalSurfaceWorldY =>
            globalSurfaceWorldY;

        public float UpperDrumLevel01 =>
            upperDrumLevel01;

        public float LowerDrumLevel01 =>
            lowerDrumLevel01;

        public float SurfaceAgitation01 =>
            surfaceAgitation01;

        public float InternalFlow01 =>
            internalFlow01;

        public float BoilingIntensity01 =>
            boilingIntensity01;
            


        /// <summary>
        /// Compatibilidade com o WaterTankSimulationController atual.
        ///
        /// Por enquanto, o nível normalizado calculado pelo modelo
        /// matemático é interpretado como inventário global normalizado.
        /// </summary>
        public void SetWaterLevel01(
            float normalizedLevel)
        {
            SetSystemFill01(
                normalizedLevel);
        }


        public void SetSystemFill01(
            float normalizedFill)
        {
            systemFill01 =
                Mathf.Clamp01(
                    normalizedFill);

            refreshRequested = true;
        }


        /// <summary>
        /// Por enquanto a circulação hidráulica alimenta tanto a
        /// agitação superficial quanto o fluxo interno.
        ///
        /// Quando refinarmos os shaders, essas duas variáveis poderão
        /// ser alimentadas independentemente.
        /// </summary>
        public void SetCirculation01(
            float normalizedCirculation)
        {
            float value =
                Mathf.Clamp01(
                    normalizedCirculation);

            surfaceAgitation01 =
                value;

            internalFlow01 =
                value;

            refreshRequested = true;
        }


        public void SetBoilingIntensity01(
            float normalizedBoiling)
        {
            boilingIntensity01 =
                Mathf.Clamp01(
                    normalizedBoiling);

            refreshRequested = true;
        }


        [ContextMenu("Apply Water Visual State")]
        public void ApplyNow()
        {
            refreshRequested = true;
            ApplyVisualState();
        }


        // ================================================================
        // UNITY
        // ================================================================

        private void OnEnable()
        {
            refreshRequested = true;

            ApplyVisualState();
        }


        private void OnValidate()
        {
            systemFill01 =
                Mathf.Clamp01(
                    systemFill01);

            emptyThreshold =
                Mathf.Max(
                    0f,
                    emptyThreshold);

            surfaceAgitation01 =
                Mathf.Clamp01(
                    surfaceAgitation01);

            internalFlow01 =
                Mathf.Clamp01(
                    internalFlow01);

            boilingIntensity01 =
                Mathf.Clamp01(
                    boilingIntensity01);

            refreshRequested = true;

            inletDisturbanceRadius =
                Mathf.Max(
                    0.01f,
                    inletDisturbanceRadius);

            farFieldAgitationFactor =
                Mathf.Clamp01(
                    farFieldAgitationFactor);

            inletAgitationGain =
                Mathf.Max(
                    0f,
                    inletAgitationGain);

            inletDisturbanceFalloff =
                Mathf.Max(
                    0.05f,
                    inletDisturbanceFalloff);
        }


        private void Update()
        {
            if (Application.isPlaying)
            {
                ApplyVisualState();
                return;
            }

            if (!refreshRequested)
                return;

            ApplyVisualState();

            refreshRequested = false;
        }


        // ================================================================
        // VISUAL STATE
        // ================================================================

        private void ApplyVisualState()
        {
            if (upperDrumWater == null ||
                lowerDrumWater == null)
            {
                return;
            }


            switch (mappingMode)
            {
                case FillMappingMode.WholeSystemNormalized:
                    ApplyWholeSystemFill();
                    break;

                case FillMappingMode.UpperDrumOperational:
                    ApplyUpperDrumOperationalFill();
                    break;
            }

            ApplyMaterialState();
        }


        // ================================================================
        // WHOLE SYSTEM MODE
        // ================================================================

        private void ApplyWholeSystemFill()
        {
            float systemBottomY =
                Mathf.Min(
                    lowerDrumWater.BottomWorldY,
                    upperDrumWater.BottomWorldY);

            float systemTopY =
                Mathf.Max(
                    lowerDrumWater.TopWorldY,
                    upperDrumWater.TopWorldY);


            globalSurfaceWorldY =
                Mathf.Lerp(
                    systemBottomY,
                    systemTopY,
                    systemFill01);


            lowerDrumLevel01 =
                EvaluateLocalFill(
                    lowerDrumWater,
                    globalSurfaceWorldY);


            upperDrumLevel01 =
                EvaluateLocalFill(
                    upperDrumWater,
                    globalSurfaceWorldY);


            ApplyDrumState(
                lowerDrumWater,
                lowerDrumLevel01);


            ApplyDrumState(
                upperDrumWater,
                upperDrumLevel01);

            if (tubeBankWater != null)
            {
                tubeBankWater.SetSurfaceWorldY(globalSurfaceWorldY);
            }
        }


        // ================================================================
        // UPPER DRUM OPERATIONAL MODE
        // ================================================================

        private void ApplyUpperDrumOperationalFill()
        {
            // Lower Drum is assumed flooded during normal boiler operation.
            lowerDrumLevel01 =
                1f;

            ApplyDrumState(
                lowerDrumWater,
                1f);


            globalSurfaceWorldY =
                Mathf.Lerp(
                    upperDrumWater.BottomWorldY,
                    upperDrumWater.TopWorldY,
                    systemFill01);


            upperDrumLevel01 =
                systemFill01;


            ApplyDrumState(
                upperDrumWater,
                upperDrumLevel01);

            if (tubeBankWater != null)
            {
                tubeBankWater.SetSurfaceWorldY(
                    globalSurfaceWorldY);
            }
        }


        // ================================================================
        // HELPERS
        // ================================================================

        private float EvaluateLocalFill(
            BoilerWaterMeshGenerator drum,
            float surfaceWorldY)
        {
            if (drum == null)
                return 0f;


            if (surfaceWorldY <=
                drum.BottomWorldY)
            {
                return 0f;
            }


            if (surfaceWorldY >=
                drum.TopWorldY)
            {
                return 1f;
            }


            return Mathf.InverseLerp(
                drum.BottomWorldY,
                drum.TopWorldY,
                surfaceWorldY);
        }


        private void ApplyDrumState(
            BoilerWaterMeshGenerator drum,
            float normalizedFill)
        {
            if (drum == null)
                return;


            float fill =
                Mathf.Clamp01(
                    normalizedFill);


            bool visible =
                fill >
                emptyThreshold;


            drum.SetVisible(
                visible);


            if (!visible)
                return;


            drum.SetLevel01(
                fill);
        }

        private void EnsurePropertyBlocks()
        {
            if (bodyPropertyBlock == null)
            {
                bodyPropertyBlock =
                    new MaterialPropertyBlock();
            }

            if (surfacePropertyBlock == null)
            {
                surfacePropertyBlock =
                    new MaterialPropertyBlock();
            }
        }

        private void ApplyMaterialStateToRenderer(
            Renderer targetRenderer,
            bool enableFeedWaterDisturbance)
        {
            if (targetRenderer == null)
                return;


            EnsurePropertyBlocks();


            // ================================================================
            // MATERIAL 0 - WATER BODY
            // ================================================================

            bodyPropertyBlock.Clear();


            bodyPropertyBlock.SetFloat(
                WaterSurfaceWorldYId,
                globalSurfaceWorldY);


            bodyPropertyBlock.SetFloat(
                SurfaceAgitation01Id,
                surfaceAgitation01);


            bodyPropertyBlock.SetFloat(
                InternalFlow01Id,
                internalFlow01);


            bodyPropertyBlock.SetFloat(
                BoilingIntensity01Id,
                boilingIntensity01);


            targetRenderer.SetPropertyBlock(
                bodyPropertyBlock,
                0);


            // ================================================================
            // MATERIAL 1 - FREE SURFACE
            // ================================================================

            surfacePropertyBlock.Clear();


            surfacePropertyBlock.SetFloat(
                WaterSurfaceWorldYId,
                globalSurfaceWorldY);


            surfacePropertyBlock.SetFloat(
                SurfaceAgitation01Id,
                surfaceAgitation01);


            surfacePropertyBlock.SetFloat(
                InternalFlow01Id,
                internalFlow01);


            surfacePropertyBlock.SetFloat(
                BoilingIntensity01Id,
                boilingIntensity01);


            // ------------------------------------------------------------
            // LOCALIZED FEED-WATER DISTURBANCE
            // ------------------------------------------------------------

            bool inletAvailable =
                enableFeedWaterDisturbance &&
                feedWaterInlet != null;


            surfacePropertyBlock.SetFloat(
                InletDisturbanceEnabledId,
                inletAvailable
                    ? 1f
                    : 0f);


            Vector3 inletPosition =
                inletAvailable
                    ? feedWaterInlet.position
                    : Vector3.zero;


            surfacePropertyBlock.SetVector(
                InletWorldPositionId,
                new Vector4(
                    inletPosition.x,
                    inletPosition.y,
                    inletPosition.z,
                    0f));


            surfacePropertyBlock.SetFloat(
                InletDisturbanceRadiusId,
                inletDisturbanceRadius);


            surfacePropertyBlock.SetFloat(
                FarFieldAgitationFactorId,
                farFieldAgitationFactor);


            surfacePropertyBlock.SetFloat(
                InletAgitationGainId,
                inletAgitationGain);


            surfacePropertyBlock.SetFloat(
                InletDisturbanceFalloffId,
                inletDisturbanceFalloff);


            targetRenderer.SetPropertyBlock(
                surfacePropertyBlock,
                1);
        }

        private void ApplyMaterialState()
        {
            EnsurePropertyBlocks();

        ApplyMaterialStateToRenderer(
            upperDrumWater != null
                ? upperDrumWater.WaterRenderer
                : null,
            true);


        ApplyMaterialStateToRenderer(
            lowerDrumWater != null
                ? lowerDrumWater.WaterRenderer
                : null,
            false);


        ApplyMaterialStateToRenderer(
            tubeBankWater != null
                ? tubeBankWater.WaterRenderer
                : null,
            false);
        }
    }

    
}