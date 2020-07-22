using System;
using System.Buffers;
using System.Numerics;

namespace VoxelWorld
{
    internal class GeometryData
    {
        private readonly Vector3[] vertices;
        private readonly Vector3[] normals;
        private readonly uint[] indices;
        private readonly int VertexCount;
        private readonly int IndiceCount;

        public Span<Vector3> Vertices { get { return vertices.AsSpan(0, VertexCount); } }
        public Span<Vector3> Normals { get { return normals.AsSpan(0, VertexCount); } }
        public Span<uint> Indices { get { return indices.AsSpan(0, IndiceCount); } }

        public GeometryData(Vector3[] vertices, Vector3[] normals, uint[] indices)
        {
            this.vertices = vertices;
            this.normals = normals;
            this.indices = indices;
            this.VertexCount = vertices.Length;
            this.IndiceCount = indices.Length;
        }

        public GeometryData(int vertexCount, int indiceCount)
        {
            this.vertices = ArrayPool<Vector3>.Shared.Rent(vertexCount);
            this.normals = ArrayPool<Vector3>.Shared.Rent(vertexCount);
            this.indices = ArrayPool<uint>.Shared.Rent(indiceCount);
            this.VertexCount = vertexCount;
            this.IndiceCount = indiceCount;


            //Array.Fill(vertices, new Vector3(0, 0, 0));
            Array.Fill(normals, new Vector3(0, 0, 0));
            //Array.Fill(indices, 0u);
        }

        public void Reuse()
        {
            ArrayPool<Vector3>.Shared.Return(vertices);
            ArrayPool<Vector3>.Shared.Return(normals);
            ArrayPool<uint>.Shared.Return(indices);
        }
    }
}
