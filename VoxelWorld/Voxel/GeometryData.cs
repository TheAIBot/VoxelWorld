using System;
using System.Buffers;
using System.Numerics;

namespace VoxelWorld.Voxel
{
    public class GeometryData
    {
        private readonly Vector3[] vertices;
        private readonly Vector3[] normals;
        private readonly uint[] indices;
        private readonly int VertexCount;
        private readonly int IndiceCount;

        public Span<Vector3> Vertices { get { return vertices.AsSpan(0, VertexCount); } }
        public Span<Vector3> Normals { get { return normals.AsSpan(0, VertexCount); } }
        public Span<uint> Indices { get { return indices.AsSpan(0, IndiceCount); } }

        public Memory<Vector3> VerticesAsMemSpan { get { return new Memory<Vector3>(vertices, 0, VertexCount); } }
        public Memory<Vector3> NormalsAsMemSpan { get { return new Memory<Vector3>(normals, 0, VertexCount); } }
        public Memory<uint> IndicesAsMemSpan { get { return new Memory<uint>(indices, 0, IndiceCount); } }
        public int TriangleCount => Indices.Length / 3;

        public GeometryData(Vector3[] vertices, Vector3[] normals, uint[] indices)
        {
            this.vertices = vertices;
            this.normals = normals;
            this.indices = indices;
            VertexCount = vertices.Length;
            IndiceCount = indices.Length;
        }

        public GeometryData(int vertexCount, int indiceCount)
        {
            vertices = ArrayPool<Vector3>.Shared.Rent(vertexCount);
            normals = ArrayPool<Vector3>.Shared.Rent(vertexCount);
            indices = ArrayPool<uint>.Shared.Rent(indiceCount);
            VertexCount = vertexCount;
            IndiceCount = indiceCount;


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
