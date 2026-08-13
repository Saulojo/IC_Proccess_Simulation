using UnityEngine;
using UnityEngine.VFX;

namespace IndustrialSim.UnityRuntime.Visualization.Boiler
{
    /// <summary>
    /// Controla geometricamente um VFX de bolhas de um riser.
    ///
    /// Nesta etapa os valores são apenas visuais.
    /// Posteriormente Circulation e SteamGeneration serão
    /// alimentados pelo modelo matemático da caldeira.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VisualEffect))]
    public sealed class BoilerRiserVFXController : MonoBehaviour
    {
        // ================================================================
        // GEOMETRY
        // ================================================================

        [Header("Riser Geometry")]

        [SerializeField]
        private Transform riserBottom;

        [SerializeField]
        private Transform riserTop;

        [Tooltip("Raio interno útil do tubo, em metros.")]
        [SerializeField, Min(0.001f)]
        private float tubeInnerRadius = 0.03f;

        [Tooltip(
            "Margem aplicada ao raio para evitar bolhas " +
            "intersectando a parede interna.")]
        [SerializeField, Range(0.1f, 1f)]
        private float spawnRadiusFactor = 0.80f;


        // ================================================================
        // PREVIEW
        // ================================================================

        [Header("Visual Preview")]

        [SerializeField, Range(0f, 1f)]
        private float circulation01 = 0.50f;

        [SerializeField, Range(0f, 1f)]
        private float steamGeneration01 = 0.35f;


        // ================================================================
        // BUBBLES
        // ================================================================

        [Header("Bubble Configuration")]

        [SerializeField, Min(0f)]
        private float minimumBubbleRate = 5f;

        [SerializeField, Min(0f)]
        private float maximumBubbleRate = 100f;

        [SerializeField, Min(0.01f)]
        private float minimumBubbleSpeed = 0.30f;

        [SerializeField, Min(0.01f)]
        private float maximumBubbleSpeed = 0.90f;

        [SerializeField, Min(0.0001f)]
        private float minimumBubbleSize = 0.004f;

        [SerializeField, Min(0.0001f)]
        private float maximumBubbleSize = 0.012f;


        // ================================================================
        // INTERNAL
        // ================================================================

        private VisualEffect visualEffect;

        private bool refreshRequested = true;


        // ================================================================
        // VFX PROPERTY IDS
        // ================================================================

        private static readonly int BubbleRateId =
            Shader.PropertyToID("BubbleRate");

        private static readonly int BubbleSpeedId =
            Shader.PropertyToID("BubbleSpeed");

        private static readonly int TubeRadiusId =
            Shader.PropertyToID("TubeRadius");

        private static readonly int TubeHeightId =
            Shader.PropertyToID("TubeHeight");

        private static readonly int BubbleMinSizeId =
            Shader.PropertyToID("BubbleMinSize");

        private static readonly int BubbleMaxSizeId =
            Shader.PropertyToID("BubbleMaxSize");

        private static readonly int Circulation01Id =
            Shader.PropertyToID("Circulation01");


        // ================================================================
        // PUBLIC
        // ================================================================

        public float Circulation01 => circulation01;

        public float SteamGeneration01 => steamGeneration01;

        public float RiserHeight
        {
            get
            {
                if (riserBottom == null || riserTop == null)
                    return 0f;

                return Vector3.Distance(
                    riserBottom.position,
                    riserTop.position);
            }
        }


        // ================================================================
        // UNITY
        // ================================================================

        private void OnEnable()
        {
            CacheComponents();
            refreshRequested = true;
        }

        private void OnValidate()
        {
            circulation01 =
                Mathf.Clamp01(circulation01);

            steamGeneration01 =
                Mathf.Clamp01(steamGeneration01);

            tubeInnerRadius =
                Mathf.Max(
                    0.001f,
                    tubeInnerRadius);

            maximumBubbleRate =
                Mathf.Max(
                    minimumBubbleRate,
                    maximumBubbleRate);

            maximumBubbleSpeed =
                Mathf.Max(
                    minimumBubbleSpeed,
                    maximumBubbleSpeed);

            maximumBubbleSize =
                Mathf.Max(
                    minimumBubbleSize,
                    maximumBubbleSize);

            refreshRequested = true;
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
        // PUBLIC API
        // ================================================================

        public void SetVisualState(
            float normalizedCirculation,
            float normalizedSteamGeneration)
        {
            circulation01 =
                Mathf.Clamp01(
                    normalizedCirculation);

            steamGeneration01 =
                Mathf.Clamp01(
                    normalizedSteamGeneration);

            ApplyVisualState();
        }


        [ContextMenu("Apply Riser VFX Now")]
        public void ApplyVisualState()
        {
            CacheComponents();

            if (!HasRequiredReferences())
                return;

            Vector3 riserVector =
                riserTop.position -
                riserBottom.position;

            float riserHeight =
                riserVector.magnitude;

            if (riserHeight <= 0.001f)
                return;

            Vector3 riserDirection =
                riserVector / riserHeight;


            // ------------------------------------------------------------
            // POSITION / ORIENTATION
            // ------------------------------------------------------------

            transform.SetPositionAndRotation(
                riserBottom.position,
                Quaternion.FromToRotation(
                    Vector3.up,
                    riserDirection));

            // Importante:
            // não escalar o VFX.
            transform.localScale =
                Vector3.one;


            // ------------------------------------------------------------
            // BUBBLE BEHAVIOUR
            // ------------------------------------------------------------

            float activeIntensity =
                Mathf.Clamp01(
                    circulation01 *
                    steamGeneration01);

            float bubbleRate =
                Mathf.Lerp(
                    minimumBubbleRate,
                    maximumBubbleRate,
                    activeIntensity);

            float bubbleSpeed =
                Mathf.Lerp(
                    minimumBubbleSpeed,
                    maximumBubbleSpeed,
                    circulation01);

            float effectiveTubeRadius =
                tubeInnerRadius *
                spawnRadiusFactor;


            // ------------------------------------------------------------
            // VFX PROPERTIES
            // ------------------------------------------------------------

            if (visualEffect.HasFloat(BubbleRateId))
            {
                visualEffect.SetFloat(
                    BubbleRateId,
                    bubbleRate);
            }

            if (visualEffect.HasFloat(BubbleSpeedId))
            {
                visualEffect.SetFloat(
                    BubbleSpeedId,
                    bubbleSpeed);
            }

            if (visualEffect.HasFloat(TubeRadiusId))
            {
                visualEffect.SetFloat(
                    TubeRadiusId,
                    effectiveTubeRadius);
            }

            if (visualEffect.HasFloat(TubeHeightId))
            {
                visualEffect.SetFloat(
                    TubeHeightId,
                    riserHeight);
            }

            if (visualEffect.HasFloat(BubbleMinSizeId))
            {
                visualEffect.SetFloat(
                    BubbleMinSizeId,
                    minimumBubbleSize);
            }

            if (visualEffect.HasFloat(BubbleMaxSizeId))
            {
                visualEffect.SetFloat(
                    BubbleMaxSizeId,
                    maximumBubbleSize);
            }

            if (visualEffect.HasFloat(Circulation01Id))
            {
                visualEffect.SetFloat(
                    Circulation01Id,
                    activeIntensity);
            }
        }


        // ================================================================
        // HELPERS
        // ================================================================

        private void CacheComponents()
        {
            if (visualEffect == null)
            {
                visualEffect =
                    GetComponent<VisualEffect>();
            }
        }

        private bool HasRequiredReferences()
        {
            return
                visualEffect != null
                && riserBottom != null
                && riserTop != null;
        }


        // ================================================================
        // GIZMOS
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            if (riserBottom == null || riserTop == null)
                return;

            Gizmos.DrawLine(
                riserBottom.position,
                riserTop.position);

            Gizmos.DrawWireSphere(
                riserBottom.position,
                tubeInnerRadius);

            Gizmos.DrawWireSphere(
                riserTop.position,
                tubeInnerRadius);
        }
    }
}