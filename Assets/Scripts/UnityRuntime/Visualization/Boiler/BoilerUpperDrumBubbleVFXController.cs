using UnityEngine;
using UnityEngine.VFX;

namespace IndustrialSim.UnityRuntime.Visualization.Boiler
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VisualEffect))]
    public sealed class BoilerUpperDrumBubbleVFXController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private BoilerWaterPreviewController waterPreview;

        [SerializeField]
        private Transform bubbleSource;

        [Header("Spawn")]
        [SerializeField, Min(0.001f)]
        private float spawnRadius = 0.07f;

        [Header("Bubble Rate")]
        [SerializeField, Min(0f)]
        private float minimumBubbleRate = 10f;

        [SerializeField, Min(0f)]
        private float maximumBubbleRate = 120f;

        [Header("Bubble Speed")]
        [SerializeField, Min(0.01f)]
        private float minimumBubbleSpeed = 0.20f;

        [SerializeField, Min(0.01f)]
        private float maximumBubbleSpeed = 0.55f;

        [Header("Bubble Size")]
        [SerializeField, Min(0.0001f)]
        private float minimumBubbleSize = 0.006f;

        [SerializeField, Min(0.0001f)]
        private float maximumBubbleSize = 0.018f;

        [Header("Preview")]
        [SerializeField, Range(0f, 1f)]
        private float steamGeneration01 = 0.50f;

        private VisualEffect visualEffect;

        private static readonly int BubbleRateId =
            Shader.PropertyToID("BubbleRate");

        private static readonly int BubbleSpeedId =
            Shader.PropertyToID("BubbleSpeed");

        private static readonly int SpawnRadiusId =
            Shader.PropertyToID("SpawnRadius");

        private static readonly int RiseDistanceId =
            Shader.PropertyToID("RiseDistance");

        private static readonly int BubbleMinSizeId =
            Shader.PropertyToID("BubbleMinSize");

        private static readonly int BubbleMaxSizeId =
            Shader.PropertyToID("BubbleMaxSize");

        private static readonly int Intensity01Id =
            Shader.PropertyToID("Intensity01");

        private void OnEnable()
        {
            visualEffect =
                GetComponent<VisualEffect>();
        }

        private void Update()
        {
            ApplyVisualState();
        }

        private void OnValidate()
        {
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
        }

        private void ApplyVisualState()
        {
            if (visualEffect == null)
            {
                visualEffect =
                    GetComponent<VisualEffect>();
            }

            if (visualEffect == null ||
                waterPreview == null ||
                bubbleSource == null)
            {
                return;
            }

            float surfaceWorldY =
                waterPreview.CurrentWaterLevelWorldY;

            float sourceWorldY =
                bubbleSource.position.y;

            float riseDistance =
                surfaceWorldY -
                sourceWorldY;

            bool sourceIsSubmerged =
                riseDistance > 0.01f;

            float circulation =
                waterPreview.Circulation01;

            float intensity =
                sourceIsSubmerged
                    ? Mathf.Clamp01(
                        circulation *
                        steamGeneration01)
                    : 0f;

            float bubbleRate =
                Mathf.Lerp(
                    minimumBubbleRate,
                    maximumBubbleRate,
                    steamGeneration01);

            float bubbleSpeed =
                Mathf.Lerp(
                    minimumBubbleSpeed,
                    maximumBubbleSpeed,
                    circulation);

            // O efeito sempre nasce no outlet do riser.
            transform.SetPositionAndRotation(
                bubbleSource.position,
                Quaternion.identity);

            transform.localScale =
                Vector3.one;

            SetFloatIfAvailable(
                BubbleRateId,
                bubbleRate);

            SetFloatIfAvailable(
                BubbleSpeedId,
                bubbleSpeed);

            SetFloatIfAvailable(
                SpawnRadiusId,
                spawnRadius);

            SetFloatIfAvailable(
                RiseDistanceId,
                Mathf.Max(
                    0f,
                    riseDistance));

            SetFloatIfAvailable(
                BubbleMinSizeId,
                minimumBubbleSize);

            SetFloatIfAvailable(
                BubbleMaxSizeId,
                maximumBubbleSize);

            SetFloatIfAvailable(
                Intensity01Id,
                intensity);
        }

        private void SetFloatIfAvailable(
            int propertyId,
            float value)
        {
            if (visualEffect.HasFloat(propertyId))
            {
                visualEffect.SetFloat(
                    propertyId,
                    value);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bubbleSource == null)
                return;

            Gizmos.DrawWireSphere(
                bubbleSource.position,
                spawnRadius);

            if (waterPreview == null)
                return;

            Vector3 surfacePoint =
                bubbleSource.position;

            surfacePoint.y =
                waterPreview.CurrentWaterLevelWorldY;

            Gizmos.DrawLine(
                bubbleSource.position,
                surfacePoint);

            Gizmos.DrawWireSphere(
                surfacePoint,
                0.025f);
        }
    }
}