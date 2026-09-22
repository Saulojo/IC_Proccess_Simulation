using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndustrialSim.UnityRuntime.Visualization.BoilerWater
{
    /// <summary>
    /// Generates all water geometry inside the boiler tube bank
    /// as a single procedural Mesh.
    ///
    /// Expected reference hierarchy:
    ///
    /// TubeBank
    /// ├── Tube_01
    /// │   ├── Bottom
    /// │   └── Top
    /// ├── Tube_02
    /// │   ├── Bottom
    /// │   └── Top
    /// ...
    ///
    /// Submesh 0:
    ///     Water body inside every tube.
    ///
    /// Submesh 1:
    ///     Free surfaces of partially filled tubes.
    ///
    /// Empty/full tubes use degenerate surface geometry instead of
    /// changing topology, allowing the same index buffers to be reused.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BoilerTubeBankWaterMeshGenerator : MonoBehaviour
    {
        private const float PositionEpsilon = 0.000001f;

        // ================================================================
        // INTERNAL TUBE DESCRIPTION
        // ================================================================

        private sealed class TubeRuntimeData
        {
            public Transform root;
            public Transform bottom;
            public Transform top;

            public Vector3 cachedBottomPosition;
            public Vector3 cachedTopPosition;

            public int sideVertexOffset;
            public int surfaceVertexOffset;
        }


        // ================================================================
        // REFERENCES
        // ================================================================

        [Header("Tube References")]

        [Tooltip(
            "Root containing Tube_01, Tube_02, etc. " +
            "Each tube must contain children named Bottom and Top.")]
        [SerializeField]
        private Transform tubeReferencesRoot;


        // ================================================================
        // GEOMETRY
        // ================================================================

        [Header("Tube Geometry")]

        [Tooltip("Internal usable radius of the water tubes in meters.")]
        [SerializeField, Min(0.001f)]
        private float tubeInnerRadius = 0.025f;

        [Tooltip(
            "Small offset applied at both tube ends so the tube water " +
            "overlaps slightly with drum water.")]
        [SerializeField, Min(0f)]
        private float connectionOverlap = 0.01f;

        [Tooltip(
            "Number of radial segments around each water cylinder.")]
        [SerializeField, Range(6, 32)]
        private int radialSegments = 12;


        // ================================================================
        // GLOBAL WATER LEVEL
        // ================================================================

        [Header("Water Level")]

        [SerializeField]
        private float surfaceWorldY;

        [SerializeField, Min(0.00001f)]
        private float minimumSurfaceDeltaToRefresh = 0.0005f;


        // ================================================================
        // MATERIALS
        // ================================================================

        [Header("Materials")]

        [SerializeField]
        private Material bodyMaterial;

        [SerializeField]
        private Material surfaceMaterial;


        // ================================================================
        // MONITORING
        // ================================================================

        [Header("Generated Geometry - Read Only")]

        [SerializeField]
        private int detectedTubeCount;

        [SerializeField]
        private int emptyTubeCount;

        [SerializeField]
        private int partiallyFilledTubeCount;

        [SerializeField]
        private int fullTubeCount;


        // ================================================================
        // COMPONENTS
        // ================================================================

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh generatedMesh;


        // ================================================================
        // RUNTIME DATA
        // ================================================================

        private readonly List<TubeRuntimeData> tubes =
            new List<TubeRuntimeData>();


        // ================================================================
        // MESH DATA
        // ================================================================

        private Vector3[] vertices;
        private Vector3[] normals;
        private Vector4[] tangents;
        private Vector2[] uv0;

        private int[] bodyTriangles;
        private int[] surfaceTriangles;


        // ================================================================
        // DIRTY STATE
        // ================================================================

        private bool referencesDirty = true;
        private bool topologyDirty = true;
        private bool geometryDirty = true;
        private bool materialsDirty = true;

        private float cachedSurfaceWorldY =
            float.NaN;

        private float cachedTubeInnerRadius =
            float.NaN;

        private float cachedConnectionOverlap =
            float.NaN;

        private int cachedRadialSegments = -1;


        // ================================================================
        // PUBLIC API
        // ================================================================

        public int TubeCount =>
            tubes.Count;

        public float SurfaceWorldY =>
            surfaceWorldY;

        public Renderer WaterRenderer
        {
            get
            {
                CacheComponents();
                return meshRenderer;
            }
        }


        public void SetSurfaceWorldY(
            float worldY)
        {
            if (Mathf.Abs(
                    worldY -
                    surfaceWorldY) <
                minimumSurfaceDeltaToRefresh)
            {
                return;
            }

            surfaceWorldY =
                worldY;

            geometryDirty =
                true;
        }


        [ContextMenu("Refresh Tube References")]
        public void RefreshTubeReferences()
        {
            referencesDirty = true;
            topologyDirty = true;
            geometryDirty = true;

            RefreshMesh();
        }


        [ContextMenu("Rebuild Tube Water Mesh")]
        public void RebuildNow()
        {
            referencesDirty = true;
            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;

            RefreshMesh();
        }


        // ================================================================
        // UNITY
        // ================================================================

        private void OnEnable()
        {
            CacheComponents();
            EnsureGeneratedMesh();

            referencesDirty = true;
            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;

            RefreshMesh();
        }


        private void OnValidate()
        {
            tubeInnerRadius =
                Mathf.Max(
                    0.001f,
                    tubeInnerRadius);

            connectionOverlap =
                Mathf.Max(
                    0f,
                    connectionOverlap);

            radialSegments =
                Mathf.Clamp(
                    radialSegments,
                    6,
                    32);

            minimumSurfaceDeltaToRefresh =
                Mathf.Max(
                    0.00001f,
                    minimumSurfaceDeltaToRefresh);

            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;
        }


        private void Update()
        {
            DetectChanges();

            if (!referencesDirty &&
                !topologyDirty &&
                !geometryDirty &&
                !materialsDirty)
            {
                return;
            }

            RefreshMesh();
        }


        private void OnDisable()
        {
            DestroyGeneratedMesh();
        }


        // ================================================================
        // REFRESH
        // ================================================================

        private void RefreshMesh()
        {
            CacheComponents();
            EnsureGeneratedMesh();

            if (referencesDirty)
            {
                ScanTubeReferences();
            }

            if (tubes.Count == 0)
            {
                ClearMesh();

                referencesDirty = false;
                topologyDirty = false;
                geometryDirty = false;

                return;
            }

            // The combined geometry is stored directly in world-aligned
            // local coordinates relative to this object.
            //
            // Keep the object transform neutral.
            transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            transform.localScale =
                Vector3.one;


            if (topologyDirty)
            {
                BuildTopology();
            }

            UpdateVertexData();
            UploadMesh();

            if (materialsDirty)
            {
                ApplyMaterials();
            }

            CacheCurrentState();

            referencesDirty = false;
            topologyDirty = false;
            geometryDirty = false;
            materialsDirty = false;
        }


        // ================================================================
        // REFERENCE SCANNING
        // ================================================================

        private void ScanTubeReferences()
        {
            tubes.Clear();

            if (tubeReferencesRoot == null)
            {
                detectedTubeCount = 0;
                return;
            }


            for (int i = 0;
                 i < tubeReferencesRoot.childCount;
                 i++)
            {
                Transform tubeRoot =
                    tubeReferencesRoot.GetChild(i);

                Transform bottom =
                    FindDirectChild(
                        tubeRoot,
                        "Bottom");

                Transform top =
                    FindDirectChild(
                        tubeRoot,
                        "Top");


                if (bottom == null ||
                    top == null)
                {
                    Debug.LogWarning(
                        $"Tube reference '{tubeRoot.name}' " +
                        "does not contain both Bottom and Top.",
                        tubeRoot);

                    continue;
                }


                Vector3 direction =
                    top.position -
                    bottom.position;


                if (direction.sqrMagnitude <=
                    PositionEpsilon)
                {
                    Debug.LogWarning(
                        $"Tube reference '{tubeRoot.name}' has " +
                        "Bottom and Top at the same position.",
                        tubeRoot);

                    continue;
                }


                tubes.Add(
                    new TubeRuntimeData
                    {
                        root =
                            tubeRoot,

                        bottom =
                            bottom,

                        top =
                            top
                    });
            }


            detectedTubeCount =
                tubes.Count;


            topologyDirty =
                true;

            geometryDirty =
                true;
        }


        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null)
                return null;


            for (int i = 0;
                 i < parent.childCount;
                 i++)
            {
                Transform child =
                    parent.GetChild(i);

                if (string.Equals(
                        child.name,
                        childName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }


            return null;
        }


        // ================================================================
        // TOPOLOGY
        // ================================================================

        private void BuildTopology()
        {
            int tubeCount =
                tubes.Count;


            // For each tube:
            //
            // Side:
            //     2 rings × (radialSegments + 1)
            //
            // Surface:
            //     center + ring(radialSegments + 1)
            //
            int sideVerticesPerTube =
                2 *
                (radialSegments + 1);

            int surfaceVerticesPerTube =
                radialSegments + 2;


            int verticesPerTube =
                sideVerticesPerTube +
                surfaceVerticesPerTube;


            int totalVertexCount =
                tubeCount *
                verticesPerTube;


            vertices =
                new Vector3[
                    totalVertexCount];

            normals =
                new Vector3[
                    totalVertexCount];

            tangents =
                new Vector4[
                    totalVertexCount];

            uv0 =
                new Vector2[
                    totalVertexCount];


            int bodyIndexCountPerTube =
                radialSegments *
                6;

            int surfaceIndexCountPerTube =
                radialSegments *
                3;


            bodyTriangles =
                new int[
                    tubeCount *
                    bodyIndexCountPerTube];

            surfaceTriangles =
                new int[
                    tubeCount *
                    surfaceIndexCountPerTube];


            int bodyCursor =
                0;

            int surfaceCursor =
                0;


            for (int tubeIndex = 0;
                 tubeIndex < tubeCount;
                 tubeIndex++)
            {
                TubeRuntimeData tube =
                    tubes[tubeIndex];


                int baseVertex =
                    tubeIndex *
                    verticesPerTube;


                tube.sideVertexOffset =
                    baseVertex;


                tube.surfaceVertexOffset =
                    baseVertex +
                    sideVerticesPerTube;


                // --------------------------------------------------------
                // CYLINDER SIDE
                // --------------------------------------------------------

                int stride =
                    radialSegments + 1;


                for (int r = 0;
                     r < radialSegments;
                     r++)
                {
                    int b0 =
                        tube.sideVertexOffset +
                        r;

                    int b1 =
                        tube.sideVertexOffset +
                        r + 1;

                    int t0 =
                        tube.sideVertexOffset +
                        stride +
                        r;

                    int t1 =
                        tube.sideVertexOffset +
                        stride +
                        r + 1;


                    bodyTriangles[bodyCursor++] =
                        b0;

                    bodyTriangles[bodyCursor++] =
                        b1;

                    bodyTriangles[bodyCursor++] =
                        t0;


                    bodyTriangles[bodyCursor++] =
                        b1;

                    bodyTriangles[bodyCursor++] =
                        t1;

                    bodyTriangles[bodyCursor++] =
                        t0;
                }


                // --------------------------------------------------------
                // FREE SURFACE
                // --------------------------------------------------------

                int center =
                    tube.surfaceVertexOffset;


                for (int r = 0;
                     r < radialSegments;
                     r++)
                {
                    int current =
                        tube.surfaceVertexOffset +
                        1 +
                        r;

                    int next =
                        current + 1;


                    surfaceTriangles[surfaceCursor++] =
                        center;

                    surfaceTriangles[surfaceCursor++] =
                        current;

                    surfaceTriangles[surfaceCursor++] =
                        next;
                }
            }
        }


        // ================================================================
        // GEOMETRY
        // ================================================================

        private void UpdateVertexData()
        {
            emptyTubeCount = 0;
            partiallyFilledTubeCount = 0;
            fullTubeCount = 0;


            for (int i = 0;
                 i < tubes.Count;
                 i++)
            {
                UpdateSingleTube(
                    tubes[i]);
            }
        }


        private void UpdateSingleTube(
            TubeRuntimeData tube)
        {
            Vector3 bottom =
                tube.bottom.position;

            Vector3 top =
                tube.top.position;


            Vector3 rawAxis =
                top -
                bottom;


            float rawLength =
                rawAxis.magnitude;


            if (rawLength <= 0.0001f)
            {
                CollapseTube(
                    tube,
                    bottom);

                emptyTubeCount++;
                return;
            }


            Vector3 axisDirection =
                rawAxis /
                rawLength;


            // ------------------------------------------------------------
            // Extend water slightly into both drums.
            // ------------------------------------------------------------

            Vector3 effectiveBottom =
                bottom -
                axisDirection *
                connectionOverlap;


            Vector3 effectiveTop =
                top +
                axisDirection *
                connectionOverlap;


            // ------------------------------------------------------------
            // Determine fill from a GLOBAL horizontal water plane.
            //
            // The tubes in this model are expected to be primarily vertical.
            // For slightly inclined tubes this center-line approximation is
            // visually sufficient because their diameter is small compared
            // with their length.
            // ------------------------------------------------------------

            float bottomY =
                effectiveBottom.y;

            float topY =
                effectiveTop.y;


            bool ascending =
                topY >=
                bottomY;


            if (!ascending)
            {
                Vector3 temporary =
                    effectiveBottom;

                effectiveBottom =
                    effectiveTop;

                effectiveTop =
                    temporary;


                bottomY =
                    effectiveBottom.y;

                topY =
                    effectiveTop.y;


                axisDirection =
                    (
                        effectiveTop -
                        effectiveBottom
                    ).normalized;
            }


            if (surfaceWorldY <= bottomY)
            {
                CollapseTube(
                    tube,
                    effectiveBottom);

                emptyTubeCount++;

                return;
            }


            bool isFull =
                surfaceWorldY >=
                topY;


            Vector3 filledTop;


            if (isFull)
            {
                filledTop =
                    effectiveTop;

                fullTubeCount++;
            }
            else
            {
                float verticalRange =
                    topY -
                    bottomY;


                float fill01;


                if (verticalRange <=
                    0.000001f)
                {
                    fill01 =
                        0f;
                }
                else
                {
                    fill01 =
                        Mathf.Clamp01(
                            (
                                surfaceWorldY -
                                bottomY
                            ) /
                            verticalRange);
                }


                filledTop =
                    Vector3.Lerp(
                        effectiveBottom,
                        effectiveTop,
                        fill01);


                // Force the exposed surface exactly onto the common
                // horizontal water plane.
                filledTop.y =
                    surfaceWorldY;


                partiallyFilledTubeCount++;
            }


            WriteTubeGeometry(
                tube,
                effectiveBottom,
                filledTop,
                axisDirection,
                !isFull);
        }


        private void WriteTubeGeometry(
            TubeRuntimeData tube,
            Vector3 bottom,
            Vector3 filledTop,
            Vector3 axisDirection,
            bool createFreeSurface)
        {
            Vector3 radialA;
            Vector3 radialB;


            BuildPerpendicularBasis(
                axisDirection,
                out radialA,
                out radialB);


            int stride =
                radialSegments + 1;


            // ------------------------------------------------------------
            // SIDE RINGS
            // ------------------------------------------------------------

            for (int r = 0;
                 r <= radialSegments;
                 r++)
            {
                float u =
                    r /
                    (float)radialSegments;


                float angle =
                    u *
                    Mathf.PI *
                    2f;


                float cos =
                    Mathf.Cos(angle);

                float sin =
                    Mathf.Sin(angle);


                Vector3 radialDirection =
                    radialA * cos +
                    radialB * sin;


                Vector3 radialOffset =
                    radialDirection *
                    tubeInnerRadius;


                int bottomIndex =
                    tube.sideVertexOffset +
                    r;


                int topIndex =
                    tube.sideVertexOffset +
                    stride +
                    r;


                vertices[bottomIndex] =
                    bottom +
                    radialOffset;


                vertices[topIndex] =
                    filledTop +
                    radialOffset;


                normals[bottomIndex] =
                    radialDirection;


                normals[topIndex] =
                    radialDirection;


                tangents[bottomIndex] =
                    new Vector4(
                        axisDirection.x,
                        axisDirection.y,
                        axisDirection.z,
                        1f);


                tangents[topIndex] =
                    tangents[bottomIndex];


                uv0[bottomIndex] =
                    new Vector2(
                        u,
                        0f);


                uv0[topIndex] =
                    new Vector2(
                        u,
                        1f);
            }


            // ------------------------------------------------------------
            // EXPOSED WATER SURFACE
            //
            // Only a partially filled tube gets a visible surface.
            //
            // Full tubes collapse this cap so that there is no artificial
            // water disc at their connection with the Upper Drum.
            // ------------------------------------------------------------

            if (createFreeSurface)
            {
                WriteFreeSurface(
                    tube,
                    filledTop);
            }
            else
            {
                CollapseSurface(
                    tube,
                    filledTop);
            }
        }


        private void WriteFreeSurface(
            TubeRuntimeData tube,
            Vector3 center)
        {
            int centerIndex =
                tube.surfaceVertexOffset;


            vertices[centerIndex] =
                center;


            normals[centerIndex] =
                Vector3.up;


            tangents[centerIndex] =
                new Vector4(
                    1f,
                    0f,
                    0f,
                    1f);


            uv0[centerIndex] =
                new Vector2(
                    0.5f,
                    0.5f);


            // Free surface is horizontal in world space.
            for (int r = 0;
                 r <= radialSegments;
                 r++)
            {
                float u =
                    r /
                    (float)radialSegments;


                float angle =
                    u *
                    Mathf.PI *
                    2f;


                float cos =
                    Mathf.Cos(angle);

                float sin =
                    Mathf.Sin(angle);


                Vector3 offset =
                    new Vector3(
                        cos *
                        tubeInnerRadius,

                        0f,

                        sin *
                        tubeInnerRadius);


                int index =
                    tube.surfaceVertexOffset +
                    1 +
                    r;


                vertices[index] =
                    center +
                    offset;


                normals[index] =
                    Vector3.up;


                tangents[index] =
                    new Vector4(
                        1f,
                        0f,
                        0f,
                        1f);


                uv0[index] =
                    new Vector2(
                        cos * 0.5f + 0.5f,
                        sin * 0.5f + 0.5f);
            }
        }


        private void CollapseTube(
            TubeRuntimeData tube,
            Vector3 point)
        {
            int sideVertexCount =
                2 *
                (radialSegments + 1);


            for (int i = 0;
                 i < sideVertexCount;
                 i++)
            {
                int index =
                    tube.sideVertexOffset +
                    i;


                vertices[index] =
                    point;

                normals[index] =
                    Vector3.up;

                tangents[index] =
                    new Vector4(
                        1f,
                        0f,
                        0f,
                        1f);

                uv0[index] =
                    Vector2.zero;
            }


            CollapseSurface(
                tube,
                point);
        }


        private void CollapseSurface(
            TubeRuntimeData tube,
            Vector3 point)
        {
            int surfaceVertexCount =
                radialSegments + 2;


            for (int i = 0;
                 i < surfaceVertexCount;
                 i++)
            {
                int index =
                    tube.surfaceVertexOffset +
                    i;


                vertices[index] =
                    point;

                normals[index] =
                    Vector3.up;

                tangents[index] =
                    new Vector4(
                        1f,
                        0f,
                        0f,
                        1f);

                uv0[index] =
                    Vector2.zero;
            }
        }


        private static void BuildPerpendicularBasis(
            Vector3 axis,
            out Vector3 radialA,
            out Vector3 radialB)
        {
            Vector3 reference =
                Mathf.Abs(
                    Vector3.Dot(
                        axis,
                        Vector3.up)) >
                0.95f

                    ? Vector3.right
                    : Vector3.up;


            radialA =
                Vector3.Cross(
                    axis,
                    reference).normalized;


            radialB =
                Vector3.Cross(
                    axis,
                    radialA).normalized;
        }


        // ================================================================
        // MESH UPLOAD
        // ================================================================

        private void UploadMesh()
        {
            if (generatedMesh == null ||
                vertices == null)
            {
                return;
            }


            if (topologyDirty)
            {
                generatedMesh.Clear();
            }


            generatedMesh.SetVertices(
                vertices);

            generatedMesh.SetNormals(
                normals);

            generatedMesh.SetTangents(
                tangents);

            generatedMesh.SetUVs(
                0,
                uv0);


            if (topologyDirty)
            {
                generatedMesh.subMeshCount =
                    2;


                generatedMesh.SetTriangles(
                    bodyTriangles,
                    0,
                    true);


                generatedMesh.SetTriangles(
                    surfaceTriangles,
                    1,
                    true);
            }


            generatedMesh.RecalculateBounds();
        }


        // ================================================================
        // MATERIALS
        // ================================================================

        private void ApplyMaterials()
        {
            if (meshRenderer == null)
                return;


            meshRenderer.sharedMaterials =
                new[]
                {
                    bodyMaterial,
                    surfaceMaterial
                };
        }


        // ================================================================
        // CHANGE DETECTION
        // ================================================================

        private void DetectChanges()
        {
            if (tubeReferencesRoot == null)
                return;


            if (radialSegments !=
                cachedRadialSegments)
            {
                topologyDirty = true;
                geometryDirty = true;
            }


            if (!Mathf.Approximately(
                    tubeInnerRadius,
                    cachedTubeInnerRadius) ||
                !Mathf.Approximately(
                    connectionOverlap,
                    cachedConnectionOverlap))
            {
                geometryDirty = true;
            }


            if (
                float.IsNaN(
                    cachedSurfaceWorldY) ||
                Mathf.Abs(
                    surfaceWorldY -
                    cachedSurfaceWorldY) >=
                minimumSurfaceDeltaToRefresh)
            {
                geometryDirty = true;
            }


            if (tubeReferencesRoot.childCount !=
                tubes.Count)
            {
                referencesDirty = true;
                topologyDirty = true;
                geometryDirty = true;

                return;
            }


            for (int i = 0;
                 i < tubes.Count;
                 i++)
            {
                TubeRuntimeData tube =
                    tubes[i];


                if (tube.bottom == null ||
                    tube.top == null)
                {
                    referencesDirty = true;
                    topologyDirty = true;
                    geometryDirty = true;

                    return;
                }


                if (
                    (
                        tube.bottom.position -
                        tube.cachedBottomPosition
                    ).sqrMagnitude >
                    PositionEpsilon ||
                    (
                        tube.top.position -
                        tube.cachedTopPosition
                    ).sqrMagnitude >
                    PositionEpsilon)
                {
                    geometryDirty = true;
                }
            }
        }


        private void CacheCurrentState()
        {
            cachedSurfaceWorldY =
                surfaceWorldY;

            cachedTubeInnerRadius =
                tubeInnerRadius;

            cachedConnectionOverlap =
                connectionOverlap;

            cachedRadialSegments =
                radialSegments;


            for (int i = 0;
                 i < tubes.Count;
                 i++)
            {
                TubeRuntimeData tube =
                    tubes[i];


                if (tube.bottom != null)
                {
                    tube.cachedBottomPosition =
                        tube.bottom.position;
                }

                if (tube.top != null)
                {
                    tube.cachedTopPosition =
                        tube.top.position;
                }
            }
        }


        // ================================================================
        // MESH MANAGEMENT
        // ================================================================

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter =
                    GetComponent<MeshFilter>();
            }


            if (meshRenderer == null)
            {
                meshRenderer =
                    GetComponent<MeshRenderer>();
            }
        }


        private void EnsureGeneratedMesh()
        {
            if (generatedMesh != null)
                return;


            generatedMesh =
                new Mesh
                {
                    name =
                        "BoilerTubeBankWaterV2_RuntimeMesh",

                    hideFlags =
                        HideFlags.DontSave
                };


            generatedMesh.MarkDynamic();


            if (meshFilter != null)
            {
                meshFilter.sharedMesh =
                    generatedMesh;
            }
        }


        private void ClearMesh()
        {
            if (generatedMesh != null)
            {
                generatedMesh.Clear();
            }
        }


        private void DestroyGeneratedMesh()
        {
            if (generatedMesh == null)
                return;


            if (meshFilter != null &&
                meshFilter.sharedMesh ==
                generatedMesh)
            {
                meshFilter.sharedMesh =
                    null;
            }


            if (Application.isPlaying)
            {
                Destroy(
                    generatedMesh);
            }
            else
            {
                DestroyImmediate(
                    generatedMesh);
            }


            generatedMesh =
                null;
        }


        // ================================================================
        // GIZMOS
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            if (tubeReferencesRoot == null)
                return;


            for (int i = 0;
                 i < tubeReferencesRoot.childCount;
                 i++)
            {
                Transform tubeRoot =
                    tubeReferencesRoot.GetChild(i);


                Transform bottom =
                    FindDirectChild(
                        tubeRoot,
                        "Bottom");


                Transform top =
                    FindDirectChild(
                        tubeRoot,
                        "Top");


                if (bottom == null ||
                    top == null)
                {
                    continue;
                }


                Gizmos.DrawLine(
                    bottom.position,
                    top.position);


                Gizmos.DrawWireSphere(
                    bottom.position,
                    tubeInnerRadius);


                Gizmos.DrawWireSphere(
                    top.position,
                    tubeInnerRadius);
            }
        }
    }
}