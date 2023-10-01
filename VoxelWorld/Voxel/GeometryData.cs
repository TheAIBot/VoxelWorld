using System;
using System.Buffers;
using System.Numerics;

namespace VoxelWorld.Voxel
{
    public sealed class GeometryData
    {
        private readonly Vector3[] vertices;
        private readonly byte[] normals;
        private readonly uint[] indices;
        private readonly int VertexCount;
        private readonly int IndiceCount;

        public Span<Vector3> Vertices { get { return vertices.AsSpan(0, VertexCount); } }
        public Span<byte> Normals { get { return normals.AsSpan(0, VertexCount); } }
        public Span<uint> Indices { get { return indices.AsSpan(0, IndiceCount); } }

        public Memory<Vector3> VerticesAsMemSpan { get { return new Memory<Vector3>(vertices, 0, VertexCount); } }
        public Memory<byte> NormalsAsMemSpan { get { return new Memory<byte>(normals, 0, VertexCount); } }
        public Memory<uint> IndicesAsMemSpan { get { return new Memory<uint>(indices, 0, IndiceCount); } }
        public int TriangleCount => Indices.Length / 3;

        public GeometryData(Vector3[] vertices, byte[] normals, uint[] indices)
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
            normals = ArrayPool<byte>.Shared.Rent(vertexCount);
            indices = ArrayPool<uint>.Shared.Rent(indiceCount);
            VertexCount = vertexCount;
            IndiceCount = indiceCount;
        }

        public void Reuse()
        {
            ArrayPool<Vector3>.Shared.Return(vertices);
            ArrayPool<byte>.Shared.Return(normals);
            ArrayPool<uint>.Shared.Return(indices);
        }
    }
}
