using UnityEngine;
using UnityEngine.VFX;

namespace IndustrialSim.UnityRuntime.Visualization.Boiler
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VisualEffect))]
    public sealed class BoilerSurfaceVFXController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private BoilerWaterPreviewController waterPreview;

        [SerializeField]
        private Transform upperDrumAxisStart;

        [SerializeField]
        private Transform upperDrumAxisEnd;

        [Header("Drum Geometry")]
        [SerializeField, Min(0.01f)]
        private float upperDrumInnerRadius = 0.52f;

        [SerializeField, Min(0f)]
        private float endInset = 0.025f;

        [SerializeField, Min(0f)]
        private float radialInset = 0.02f;

        [SerializeField, Min(0f)]
        private float verticalOffset = 0.005f;

        [Header("Effect")]
        [SerializeField, Min(0f)]
        private float foamRate = 35f;

        [SerializeField, Min(0f)]
        private float dropletRate = 12f;

        private VisualEffect visualEffect;

        private static readonly int Intensity01Id =
            Shader.PropertyToID("Intensity01");

        private static readonly int SurfaceLengthId =
            Shader.PropertyToID("SurfaceLength");

        private static readonly int SurfaceWidthId =
            Shader.PropertyToID("SurfaceWidth");

        private static readonly int FoamRateId =
            Shader.PropertyToID("FoamRate");

        private static readonly int DropletRateId =
            Shader.PropertyToID("DropletRate");

        private void OnEnable()
        {
            visualEffect =
                GetComponent<VisualEffect>();
        }

        private void Update()
        {
            ApplyVisualState();
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
                upperDrumAxisStart == null ||
                upperDrumAxisEnd == null)
            {
                return;
            }

            float levelY =
                waterPreview.CurrentWaterLevelWorldY;

            Vector3 axis =
                upperDrumAxisEnd.position -
                upperDrumAxisStart.position;

            Vector3 horizontalAxis =
                Vector3.ProjectOnPlane(
                    axis,
                    Vector3.up);

            if (horizontalAxis.sqrMagnitude <= 0.000001f)
                return;

            float length =
                Mathf.Max(
                    0.001f,
                    horizontalAxis.magnitude -
                    endInset * 2f);

            Vector3 center =
                (
                    upperDrumAxisStart.position +
                    upperDrumAxisEnd.position
                ) * 0.5f;

            float drumCenterY =
                center.y;

            float levelOffset =
                levelY - drumCenterY;

            float halfChordSquared =
                upperDrumInnerRadius *
                upperDrumInnerRadius -
                levelOffset *
                levelOffset;

            float halfChord =
                Mathf.Sqrt(
                    Mathf.Max(
                        0f,
                        halfChordSquared));

            float width =
                Mathf.Max(
                    0.001f,
                    halfChord * 2f -
                    radialInset * 2f);

            Vector3 lengthDirection =
                horizontalAxis.normalized;

            Vector3 forwardDirection =
                Vector3.Cross(
                    lengthDirection,
                    Vector3.up).normalized;

            center.y =
                levelY + verticalOffset;

            transform.SetPositionAndRotation(
                center,
                Quaternion.LookRotation(
                    forwardDirection,
                    Vector3.up));

            transform.localScale =
                Vector3.one;

            float intensity =
                Mathf.Clamp01(
                    waterPreview.BoilingIntensity01 *
                    0.75f +
                    waterPreview.Circulation01 *
                    0.25f);

            SetFloat(
                Intensity01Id,
                intensity);

            SetFloat(
                SurfaceLengthId,
                length);

            SetFloat(
                SurfaceWidthId,
                width);

            SetFloat(
                FoamRateId,
                foamRate);

            SetFloat(
                DropletRateId,
                dropletRate);
        }

        private void SetFloat(
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
    }
}