using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelWorld.Voxel
{
    public sealed class GeometryData
    {
        private readonly byte[] normals;
        private readonly uint[] indices;
        private readonly int VertexCount;
        private readonly int IndiceCount;

        public Vector3 GridTopLeftPosition { get; }
        public float GridSize { get; }
        public int Size { get; }
        public Span<byte> Normals { get { return normals.AsSpan(0, VertexCount); } }
        public Span<uint> Indices { get { return indices.AsSpan(0, IndiceCount); } }

        public Memory<byte> NormalsAsMemSpan { get { return new Memory<byte>(normals, 0, VertexCount); } }
        public Memory<uint> IndicesAsMemSpan { get { return new Memory<uint>(indices, 0, IndiceCount); } }
        public int TriangleCount => Indices.Length / 3;

        public GeometryData(Vector3 gridTopLeftPosition, float gridSize, int size, int vertexCount, int indiceCount)
        {
            GridTopLeftPosition = gridTopLeftPosition;
            GridSize = gridSize;
            Size = size;
            normals = ArrayPool<byte>.Shared.Rent(vertexCount);
            indices = ArrayPool<uint>.Shared.Rent(indiceCount);
            VertexCount = vertexCount;
            IndiceCount = indiceCount;
        }

        public int GetSizeInBytes()
        {
            return vertices.Length * Marshal.SizeOf<Vector3>() +
                   normals.Length * sizeof(byte) +
                   indices.Length * sizeof(uint);
        }

        public void Reuse()
        {
            ArrayPool<byte>.Shared.Return(normals);
            ArrayPool<uint>.Shared.Return(indices);
        }
    }
}