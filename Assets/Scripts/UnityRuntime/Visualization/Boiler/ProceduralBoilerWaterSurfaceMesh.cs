using UnityEngine;
using UnityEngine.Rendering;

namespace IndustrialSim.UnityRuntime.Visualization.Boiler
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralBoilerWaterSurfaceMesh : MonoBehaviour
    {
        [Header("Subdivisions")]
        [SerializeField, Min(1)]
        private int lengthSegments = 96;

        [SerializeField, Min(1)]
        private int widthSegments = 40;

        private Mesh generatedMesh;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            lengthSegments = Mathf.Max(
                1,
                lengthSegments);

            widthSegments = Mathf.Max(
                1,
                widthSegments);

            Rebuild();
        }

        public void Rebuild()
        {
            MeshFilter meshFilter =
                GetComponent<MeshFilter>();

            int verticesPerRow =
                lengthSegments + 1;

            int rows =
                widthSegments + 1;

            int vertexCount =
                verticesPerRow * rows;

            int indexCount =
                lengthSegments
                * widthSegments
                * 6;

            if (generatedMesh == null)
            {
                generatedMesh =
                    new Mesh
                    {
                        name =
                            $"{name}_GeneratedWaterSurface",

                        hideFlags =
                            HideFlags.DontSave
                    };
            }

            generatedMesh.Clear();

            generatedMesh.indexFormat =
                vertexCount > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;

            Vector3[] vertices =
                new Vector3[vertexCount];

            Vector3[] normals =
                new Vector3[vertexCount];

            Vector4[] tangents =
                new Vector4[vertexCount];

            Vector2[] uvs =
                new Vector2[vertexCount];

            int[] triangles =
                new int[indexCount];

            int vertexIndex = 0;

            for (
                int z = 0;
                z <= widthSegments;
                z++)
            {
                float normalizedZ =
                    z /
                    (float)widthSegments;

                float localZ =
                    normalizedZ - 0.5f;

                for (
                    int x = 0;
                    x <= lengthSegments;
                    x++)
                {
                    float normalizedX =
                        x /
                        (float)lengthSegments;

                    float localX =
                        normalizedX - 0.5f;

                    vertices[vertexIndex] =
                        new Vector3(
                            localX,
                            0f,
                            localZ);

                    normals[vertexIndex] =
                        Vector3.up;

                    tangents[vertexIndex] =
                        new Vector4(
                            1f,
                            0f,
                            0f,
                            1f);

                    uvs[vertexIndex] =
                        new Vector2(
                            normalizedX,
                            normalizedZ);

                    vertexIndex++;
                }
            }

            int triangleIndex = 0;

            for (
                int z = 0;
                z < widthSegments;
                z++)
            {
                int currentRow =
                    z * verticesPerRow;

                int nextRow =
                    (z + 1)
                    * verticesPerRow;

                for (
                    int x = 0;
                    x < lengthSegments;
                    x++)
                {
                    int a =
                        currentRow + x;

                    int b =
                        currentRow + x + 1;

                    int c =
                        nextRow + x;

                    int d =
                        nextRow + x + 1;

                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;

                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            generatedMesh.vertices =
                vertices;

            generatedMesh.normals =
                normals;

            generatedMesh.tangents =
                tangents;

            generatedMesh.uv =
                uvs;

            generatedMesh.triangles =
                triangles;

            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh =
                generatedMesh;
        }

        private void OnDisable()
        {
            if (generatedMesh == null)
                return;

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

            generatedMesh = null;
        }
    }
}