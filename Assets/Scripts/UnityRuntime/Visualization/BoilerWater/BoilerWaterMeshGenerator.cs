using UnityEngine;

namespace IndustrialSim.UnityRuntime.Visualization.BoilerWater
{
    /// <summary>
    /// Generates the water geometry for a horizontal cylindrical drum.
    ///
    /// The generated mesh contains two submeshes:
    ///
    /// Submesh 0:
    ///     Water body
    ///     - cylindrical wetted wall
    ///     - front end cap
    ///     - rear end cap
    ///
    /// Submesh 1:
    ///     Free water surface
    ///
    /// The body and the surface meet at exactly the same geometric boundary.
    /// There is no independent surface mesh and therefore no clipping seam
    /// between two unrelated water objects.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BoilerWaterMeshGenerator : MonoBehaviour
    {
        private const float MinimumVisualLevel = 0f;
        private const float MaximumVisualLevel = 1f;

        // ================================================================
        // REFERENCES
        // ================================================================

        [Header("Drum References")]

        [Tooltip("Center-line point at one internal end of the upper drum.")]
        [SerializeField]
        private Transform axisStart;

        [Tooltip("Center-line point at the opposite internal end of the upper drum.")]
        [SerializeField]
        private Transform axisEnd;


        // ================================================================
        // DRUM GEOMETRY
        // ================================================================

        [Header("Drum Geometry")]

        [Tooltip("Internal radius of the cylindrical drum in meters.")]
        [SerializeField, Min(0.001f)]
        private float innerRadius = 0.52f;

        [Tooltip(
            "Removes a small amount of water geometry from each drum end " +
            "to prevent intersection with the metallic end caps.")]
        [SerializeField, Min(0f)]
        private float endInset = 0.01f;


        // ================================================================
        // WATER LEVEL
        // ================================================================

        [Header("Water Level")]

        [Tooltip(
            "Normalized physical water height. " +
            "0 = bottom of the drum, 1 = top of the drum.")]
        [SerializeField, Range(0f, 1f)]
        private float level01 = 0.50f;

        [Tooltip(
            "Minimum normalized level difference required before rebuilding " +
            "the vertex positions.")]
        [SerializeField, Min(0.00001f)]
        private float minimumLevelDeltaToRefresh = 0.0005f;


        // ================================================================
        // MESH QUALITY
        // ================================================================

        [Header("Mesh Quality")]

        [Tooltip(
            "Number of segments around the wetted circular arc. " +
            "48 is already very smooth for this drum.")]
        [SerializeField, Range(8, 128)]
        private int arcSegments = 48;

        [Tooltip(
            "Segments along the cylindrical body. " +
            "The body itself does not need many because it is straight.")]
        [SerializeField, Range(1, 16)]
        private int bodyLengthSegments = 1;

        [Tooltip(
            "Surface subdivisions along the drum axis. " +
            "Reserved for future small geometric surface deformation.")]
        [SerializeField, Range(1, 128)]
        private int surfaceLengthSegments = 32;

        [Tooltip(
            "Surface subdivisions across the width of the free surface.")]
        [SerializeField, Range(1, 32)]
        private int surfaceWidthSegments = 8;


        // ================================================================
        // MATERIALS
        // ================================================================

        [Header("Materials")]

        [SerializeField]
        private Material bodyMaterial;

        [SerializeField]
        private Material surfaceMaterial;


        // ================================================================
        // RUNTIME MONITORING
        // ================================================================

        [Header("Generated Geometry - Read Only")]

        [SerializeField]
        private float currentDrumLength;

        [SerializeField]
        private float currentLocalLevelY;

        [SerializeField]
        private float currentWorldLevelY;

        [SerializeField]
        private float currentSurfaceWidth;


        // ================================================================
        // COMPONENTS
        // ================================================================

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh generatedMesh;


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
        // VERTEX OFFSETS
        // ================================================================

        private int sideVertexOffset;
        private int startCapVertexOffset;
        private int endCapVertexOffset;
        private int surfaceVertexOffset;


        // ================================================================
        // DIRTY STATE
        // ================================================================

        private bool topologyDirty = true;
        private bool geometryDirty = true;
        private bool materialsDirty = true;


        // ================================================================
        // CACHED STATE
        // ================================================================

        private Vector3 cachedAxisStartPosition;
        private Vector3 cachedAxisEndPosition;

        private float cachedInnerRadius;
        private float cachedEndInset;
        private float cachedLevel01 = -1f;

        private int cachedArcSegments = -1;
        private int cachedBodyLengthSegments = -1;
        private int cachedSurfaceLengthSegments = -1;
        private int cachedSurfaceWidthSegments = -1;


        // ================================================================
        // PUBLIC API
        // ================================================================

        public float Level01 => level01;

        public Renderer WaterRenderer
        {
            get
            {
                CacheComponents();
                return meshRenderer;
            }
        }

        public float CurrentWorldLevelY => currentWorldLevelY;

        public float CurrentSurfaceWidth => currentSurfaceWidth;

        public float CenterWorldY
        {
            get
            {
                if (axisStart == null || axisEnd == null)
                    return transform.position.y;

                return
                    (
                        axisStart.position.y +
                        axisEnd.position.y
                    ) * 0.5f;
            }
        }

        public float BottomWorldY =>
            CenterWorldY - innerRadius;

        public float TopWorldY =>
            CenterWorldY + innerRadius;

        public float InnerRadius =>
            innerRadius;


        /// <summary>
        /// Sets the visual water level.
        ///
        /// This will later be called by BoilerWaterVisualController using
        /// the level calculated by WaterTankSimulationController.
        /// </summary>
        public void SetLevel01(float normalizedLevel)
        {
            float clamped =
                Mathf.Clamp01(
                    normalizedLevel);

            if (Mathf.Abs(clamped - level01) <
                minimumLevelDeltaToRefresh)
            {
                return;
            }

            level01 = clamped;
            geometryDirty = true;
        }

        /// <summary>
        /// Recebe diretamente a altura global da superfície da água
        /// e a converte para o nível local deste tambor.
        /// </summary>
        public void SetSurfaceWorldY(float surfaceWorldY)
        {
            float bottom =
                BottomWorldY;

            float top =
                TopWorldY;

            float normalizedLevel =
                Mathf.InverseLerp(
                    bottom,
                    top,
                    surfaceWorldY);

            SetLevel01(
                normalizedLevel);
        }


        /// <summary>
        /// Ativa ou desativa somente o Renderer da água.
        /// O GameObject e o mesh continuam existentes.
        /// </summary>
        public void SetVisible(bool visible)
        {
            CacheComponents();

            if (meshRenderer != null)
            {
                meshRenderer.enabled =
                    visible;
            }
        }


        [ContextMenu("Rebuild Water Mesh")]
        public void RebuildNow()
        {
            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;

            RefreshMesh();
        }


        // ================================================================
        // UNITY LIFECYCLE
        // ================================================================

        private void OnEnable()
        {
            CacheComponents();
            EnsureGeneratedMesh();

            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;

            RefreshMesh();
        }


        private void OnValidate()
        {
            innerRadius =
                Mathf.Max(
                    0.001f,
                    innerRadius);

            endInset =
                Mathf.Max(
                    0f,
                    endInset);

            level01 =
                Mathf.Clamp01(
                    level01);

            minimumLevelDeltaToRefresh =
                Mathf.Max(
                    0.00001f,
                    minimumLevelDeltaToRefresh);

            arcSegments =
                Mathf.Clamp(
                    arcSegments,
                    8,
                    128);

            bodyLengthSegments =
                Mathf.Clamp(
                    bodyLengthSegments,
                    1,
                    16);

            surfaceLengthSegments =
                Mathf.Clamp(
                    surfaceLengthSegments,
                    1,
                    128);

            surfaceWidthSegments =
                Mathf.Clamp(
                    surfaceWidthSegments,
                    1,
                    32);

            topologyDirty = true;
            geometryDirty = true;
            materialsDirty = true;
        }


        private void Update()
        {
            DetectExternalChanges();

            if (!topologyDirty &&
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
        // MAIN REFRESH
        // ================================================================

        private void RefreshMesh()
        {
            CacheComponents();
            EnsureGeneratedMesh();

            if (!TryGetDrumGeometry(
                    out Vector3 worldCenter,
                    out Vector3 axisDirection,
                    out float drumLength))
            {
                return;
            }

            UpdateWaterObjectTransform(
                worldCenter,
                axisDirection);

            if (topologyDirty)
            {
                BuildTopology();
            }

            UpdateVertexData(
                drumLength);

            UploadMeshData();

            if (materialsDirty)
            {
                ApplyMaterials();
            }

            CacheCurrentState();

            topologyDirty = false;
            geometryDirty = false;
            materialsDirty = false;
        }


        // ================================================================
        // DRUM GEOMETRY
        // ================================================================

        private bool TryGetDrumGeometry(
            out Vector3 worldCenter,
            out Vector3 axisDirection,
            out float drumLength)
        {
            worldCenter = Vector3.zero;
            axisDirection = Vector3.right;
            drumLength = 0f;

            if (axisStart == null ||
                axisEnd == null)
            {
                return false;
            }

            Vector3 rawAxis =
                axisEnd.position -
                axisStart.position;

            Vector3 horizontalAxis =
                Vector3.ProjectOnPlane(
                    rawAxis,
                    Vector3.up);

            float rawLength =
                horizontalAxis.magnitude;

            if (rawLength <= 0.001f)
            {
                return false;
            }

            worldCenter =
                (
                    axisStart.position +
                    axisEnd.position
                ) * 0.5f;

            axisDirection =
                horizontalAxis /
                rawLength;

            drumLength =
                Mathf.Max(
                    0.001f,
                    rawLength -
                    endInset * 2f);

            return true;
        }


        private void UpdateWaterObjectTransform(
            Vector3 worldCenter,
            Vector3 axisDirection)
        {
            Vector3 forwardDirection =
                Vector3.Cross(
                    axisDirection,
                    Vector3.up);

            if (forwardDirection.sqrMagnitude <=
                0.000001f)
            {
                return;
            }

            forwardDirection.Normalize();

            Quaternion worldRotation =
                Quaternion.LookRotation(
                    forwardDirection,
                    Vector3.up);

            transform.SetPositionAndRotation(
                worldCenter,
                worldRotation);

            transform.localScale =
                Vector3.one;
        }


        // ================================================================
        // TOPOLOGY
        // ================================================================

        private void BuildTopology()
        {
            int sideVertexCount =
                (bodyLengthSegments + 1) *
                (arcSegments + 1);

            int capVertexCount =
                arcSegments + 2;

            int surfaceVertexCount =
                (surfaceLengthSegments + 1) *
                (surfaceWidthSegments + 1);


            sideVertexOffset =
                0;

            startCapVertexOffset =
                sideVertexCount;

            endCapVertexOffset =
                startCapVertexOffset +
                capVertexCount;

            surfaceVertexOffset =
                endCapVertexOffset +
                capVertexCount;


            int totalVertexCount =
                sideVertexCount +
                capVertexCount * 2 +
                surfaceVertexCount;


            vertices =
                new Vector3[totalVertexCount];

            normals =
                new Vector3[totalVertexCount];

            tangents =
                new Vector4[totalVertexCount];

            uv0 =
                new Vector2[totalVertexCount];


            BuildBodyTriangleIndices();
            BuildSurfaceTriangleIndices();
        }


        private void BuildBodyTriangleIndices()
        {
            int sideTriangleIndexCount =
                bodyLengthSegments *
                arcSegments *
                6;

            int trianglesPerCap =
                arcSegments + 1;

            int capTriangleIndexCount =
                trianglesPerCap *
                3 *
                2;

            bodyTriangles =
                new int[
                    sideTriangleIndexCount +
                    capTriangleIndexCount];


            int triangleCursor = 0;


            // ------------------------------------------------------------
            // CURVED CYLINDRICAL WALL
            // ------------------------------------------------------------

            int sideStride =
                arcSegments + 1;

            for (int x = 0;
                 x < bodyLengthSegments;
                 x++)
            {
                for (int a = 0;
                     a < arcSegments;
                     a++)
                {
                    int v00 =
                        sideVertexOffset +
                        x * sideStride +
                        a;

                    int v01 =
                        v00 + 1;

                    int v10 =
                        sideVertexOffset +
                        (x + 1) * sideStride +
                        a;

                    int v11 =
                        v10 + 1;


                    bodyTriangles[triangleCursor++] =
                        v00;

                    bodyTriangles[triangleCursor++] =
                        v01;

                    bodyTriangles[triangleCursor++] =
                        v10;


                    bodyTriangles[triangleCursor++] =
                        v01;

                    bodyTriangles[triangleCursor++] =
                        v11;

                    bodyTriangles[triangleCursor++] =
                        v10;
                }
            }


            // ------------------------------------------------------------
            // START CAP
            //
            // Local -X outward direction.
            // ------------------------------------------------------------

            int startCenter =
                startCapVertexOffset;

            for (int a = 0;
                 a < arcSegments;
                 a++)
            {
                int current =
                    startCapVertexOffset +
                    1 +
                    a;

                int next =
                    current + 1;

                bodyTriangles[triangleCursor++] =
                    startCenter;

                bodyTriangles[triangleCursor++] =
                    next;

                bodyTriangles[triangleCursor++] =
                    current;
            }


            int startRight =
                startCapVertexOffset + 1;

            int startLeft =
                startCapVertexOffset +
                1 +
                arcSegments;

            bodyTriangles[triangleCursor++] =
                startCenter;

            bodyTriangles[triangleCursor++] =
                startRight;

            bodyTriangles[triangleCursor++] =
                startLeft;


            // ------------------------------------------------------------
            // END CAP
            //
            // Local +X outward direction.
            // ------------------------------------------------------------

            int endCenter =
                endCapVertexOffset;

            for (int a = 0;
                 a < arcSegments;
                 a++)
            {
                int current =
                    endCapVertexOffset +
                    1 +
                    a;

                int next =
                    current + 1;

                bodyTriangles[triangleCursor++] =
                    endCenter;

                bodyTriangles[triangleCursor++] =
                    current;

                bodyTriangles[triangleCursor++] =
                    next;
            }


            int endRight =
                endCapVertexOffset + 1;

            int endLeft =
                endCapVertexOffset +
                1 +
                arcSegments;

            bodyTriangles[triangleCursor++] =
                endCenter;

            bodyTriangles[triangleCursor++] =
                endLeft;

            bodyTriangles[triangleCursor++] =
                endRight;
        }


        private void BuildSurfaceTriangleIndices()
        {
            surfaceTriangles =
                new int[
                    surfaceLengthSegments *
                    surfaceWidthSegments *
                    6];


            int cursor = 0;

            int rowStride =
                surfaceWidthSegments + 1;


            for (int x = 0;
                 x < surfaceLengthSegments;
                 x++)
            {
                for (int z = 0;
                     z < surfaceWidthSegments;
                     z++)
                {
                    int v00 =
                        surfaceVertexOffset +
                        x * rowStride +
                        z;

                    int v01 =
                        v00 + 1;

                    int v10 =
                        surfaceVertexOffset +
                        (x + 1) * rowStride +
                        z;

                    int v11 =
                        v10 + 1;


                    surfaceTriangles[cursor++] =
                        v00;

                    surfaceTriangles[cursor++] =
                        v10;

                    surfaceTriangles[cursor++] =
                        v01;


                    surfaceTriangles[cursor++] =
                        v01;

                    surfaceTriangles[cursor++] =
                        v10;

                    surfaceTriangles[cursor++] =
                        v11;
                }
            }
        }


        // ================================================================
        // VERTEX GENERATION
        // ================================================================

        private void UpdateVertexData(
            float drumLength)
        {
            float clampedLevel =
                Mathf.Clamp01(
                    level01);

            if (clampedLevel <= 0.000001f)
            {
                clampedLevel = 0f;
            }
            else if (clampedLevel >= 0.999999f)
            {
                clampedLevel = 1f;
            }


            float radius =
                innerRadius;


            // ------------------------------------------------------------
            // Physical liquid height inside the circular cross-section.
            //
            // level01 = 0 -> -radius
            // level01 = 1 -> +radius
            // ------------------------------------------------------------

            float waterY =
                Mathf.Lerp(
                    -radius,
                    radius,
                    clampedLevel);


            float normalizedHeight =
                Mathf.Clamp(
                    waterY / radius,
                    -1f,
                    1f);


            // Right-side intersection between the horizontal liquid plane
            // and the drum circle.
            float rightAngle =
                Mathf.Asin(
                    normalizedHeight);


            // The arc follows the lower/wetted side of the circle from the
            // right intersection, through the bottom, to the left.
            float leftAngle =
                -Mathf.PI -
                rightAngle;


            float halfSurfaceWidth =
                Mathf.Sqrt(
                    Mathf.Max(
                        0f,
                        radius * radius -
                        waterY * waterY));


            float halfLength =
                drumLength * 0.5f;


            currentDrumLength =
                drumLength;

            currentLocalLevelY =
                waterY;

            currentWorldLevelY =
                transform.TransformPoint(
                    new Vector3(
                        0f,
                        waterY,
                        0f)).y;

            currentSurfaceWidth =
                halfSurfaceWidth * 2f;


            WriteCurvedBodyVertices(
                halfLength,
                radius,
                rightAngle,
                leftAngle);

            WriteEndCapVertices(
                halfLength,
                radius,
                waterY,
                rightAngle,
                leftAngle);

            WriteSurfaceVertices(
                halfLength,
                waterY,
                halfSurfaceWidth);
        }


        private void WriteCurvedBodyVertices(
            float halfLength,
            float radius,
            float rightAngle,
            float leftAngle)
        {
            int stride =
                arcSegments + 1;


            for (int x = 0;
                 x <= bodyLengthSegments;
                 x++)
            {
                float tx =
                    x /
                    (float)bodyLengthSegments;

                float localX =
                    Mathf.Lerp(
                        -halfLength,
                        halfLength,
                        tx);


                for (int a = 0;
                     a <= arcSegments;
                     a++)
                {
                    float ta =
                        a /
                        (float)arcSegments;

                    float angle =
                        Mathf.Lerp(
                            rightAngle,
                            leftAngle,
                            ta);


                    float sin =
                        Mathf.Sin(angle);

                    float cos =
                        Mathf.Cos(angle);


                    int index =
                        sideVertexOffset +
                        x * stride +
                        a;


                    vertices[index] =
                        new Vector3(
                            localX,
                            radius * sin,
                            radius * cos);


                    normals[index] =
                        new Vector3(
                            0f,
                            sin,
                            cos);


                    tangents[index] =
                        new Vector4(
                            1f,
                            0f,
                            0f,
                            1f);


                    uv0[index] =
                        new Vector2(
                            tx,
                            ta);
                }
            }
        }


        private void WriteEndCapVertices(
            float halfLength,
            float radius,
            float waterY,
            float rightAngle,
            float leftAngle)
        {
            float centerY =
                (
                    -radius +
                    waterY
                ) * 0.5f;


            WriteSingleEndCap(
                startCapVertexOffset,
                -halfLength,
                Vector3.left,
                centerY,
                radius,
                rightAngle,
                leftAngle);


            WriteSingleEndCap(
                endCapVertexOffset,
                halfLength,
                Vector3.right,
                centerY,
                radius,
                rightAngle,
                leftAngle);
        }


        private void WriteSingleEndCap(
            int offset,
            float localX,
            Vector3 normal,
            float centerY,
            float radius,
            float rightAngle,
            float leftAngle)
        {
            vertices[offset] =
                new Vector3(
                    localX,
                    centerY,
                    0f);

            normals[offset] =
                normal;

            tangents[offset] =
                new Vector4(
                    0f,
                    0f,
                    1f,
                    1f);

            uv0[offset] =
                new Vector2(
                    0.5f,
                    Mathf.InverseLerp(
                        -radius,
                        radius,
                        centerY));


            for (int a = 0;
                 a <= arcSegments;
                 a++)
            {
                float ta =
                    a /
                    (float)arcSegments;

                float angle =
                    Mathf.Lerp(
                        rightAngle,
                        leftAngle,
                        ta);


                float y =
                    radius *
                    Mathf.Sin(angle);

                float z =
                    radius *
                    Mathf.Cos(angle);


                int index =
                    offset +
                    1 +
                    a;


                vertices[index] =
                    new Vector3(
                        localX,
                        y,
                        z);

                normals[index] =
                    normal;

                tangents[index] =
                    new Vector4(
                        0f,
                        0f,
                        1f,
                        1f);

                uv0[index] =
                    new Vector2(
                        Mathf.InverseLerp(
                            -radius,
                            radius,
                            z),

                        Mathf.InverseLerp(
                            -radius,
                            radius,
                            y));
            }
        }


        private void WriteSurfaceVertices(
            float halfLength,
            float waterY,
            float halfSurfaceWidth)
        {
            int rowStride =
                surfaceWidthSegments + 1;


            for (int x = 0;
                 x <= surfaceLengthSegments;
                 x++)
            {
                float tx =
                    x /
                    (float)surfaceLengthSegments;

                float localX =
                    Mathf.Lerp(
                        -halfLength,
                        halfLength,
                        tx);


                for (int z = 0;
                     z <= surfaceWidthSegments;
                     z++)
                {
                    float tz =
                        z /
                        (float)surfaceWidthSegments;

                    float localZ =
                        Mathf.Lerp(
                            -halfSurfaceWidth,
                            halfSurfaceWidth,
                            tz);


                    int index =
                        surfaceVertexOffset +
                        x * rowStride +
                        z;


                    vertices[index] =
                        new Vector3(
                            localX,
                            waterY,
                            localZ);


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
                            tx,
                            tz);
                }
            }
        }


        // ================================================================
        // MESH UPLOAD
        // ================================================================

        private void UploadMeshData()
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
        // COMPONENT / MESH MANAGEMENT
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
                        "BoilerWaterV2_RuntimeMesh",

                    hideFlags =
                        HideFlags.DontSave
                };


            // The liquid geometry changes as the tank level changes.
            generatedMesh.MarkDynamic();


            if (meshFilter != null)
            {
                meshFilter.sharedMesh =
                    generatedMesh;
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
        // CHANGE DETECTION
        // ================================================================

        private void DetectExternalChanges()
        {
            if (axisStart == null ||
                axisEnd == null)
            {
                return;
            }


            if (arcSegments !=
                    cachedArcSegments ||
                bodyLengthSegments !=
                    cachedBodyLengthSegments ||
                surfaceLengthSegments !=
                    cachedSurfaceLengthSegments ||
                surfaceWidthSegments !=
                    cachedSurfaceWidthSegments)
            {
                topologyDirty = true;
                geometryDirty = true;
            }


            if (
                (
                    axisStart.position -
                    cachedAxisStartPosition
                ).sqrMagnitude >
                0.00000001f ||
                (
                    axisEnd.position -
                    cachedAxisEndPosition
                ).sqrMagnitude >
                0.00000001f ||
                !Mathf.Approximately(
                    innerRadius,
                    cachedInnerRadius) ||
                !Mathf.Approximately(
                    endInset,
                    cachedEndInset))
            {
                geometryDirty = true;
            }


            if (
                Mathf.Abs(
                    level01 -
                    cachedLevel01) >=
                minimumLevelDeltaToRefresh)
            {
                geometryDirty = true;
            }
        }


        private void CacheCurrentState()
        {
            if (axisStart != null)
            {
                cachedAxisStartPosition =
                    axisStart.position;
            }

            if (axisEnd != null)
            {
                cachedAxisEndPosition =
                    axisEnd.position;
            }


            cachedInnerRadius =
                innerRadius;

            cachedEndInset =
                endInset;

            cachedLevel01 =
                level01;


            cachedArcSegments =
                arcSegments;

            cachedBodyLengthSegments =
                bodyLengthSegments;

            cachedSurfaceLengthSegments =
                surfaceLengthSegments;

            cachedSurfaceWidthSegments =
                surfaceWidthSegments;
        }


        // ================================================================
        // GIZMOS
        // ================================================================

        private void OnDrawGizmosSelected()
        {
            if (axisStart == null ||
                axisEnd == null)
            {
                return;
            }


            Gizmos.DrawLine(
                axisStart.position,
                axisEnd.position);


            Gizmos.DrawWireSphere(
                axisStart.position,
                0.025f);


            Gizmos.DrawWireSphere(
                axisEnd.position,
                0.025f);


            if (currentDrumLength <= 0f)
                return;


            Vector3 levelCenter =
                transform.TransformPoint(
                    new Vector3(
                        0f,
                        currentLocalLevelY,
                        0f));


            Vector3 halfWidthVector =
                transform.TransformDirection(
                    Vector3.forward) *
                (
                    currentSurfaceWidth *
                    0.5f);


            Gizmos.DrawLine(
                levelCenter -
                halfWidthVector,
                levelCenter +
                halfWidthVector);
        }
    }
}