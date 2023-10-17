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

        public Vector3 GridTopLeftPosition { get; set; }
        public float GridSize { get; }
        public Vector3 Size { get; set; }
        public Span<byte> Normals { get { return normals.AsSpan(0, VertexCount); } }
        public Span<uint> Indices { get { return indices.AsSpan(0, IndiceCount); } }
        public int TriangleCount => Indices.Length / 3;

        public GeometryData(Vector3 gridTopLeftPosition, float gridSize, Vector3 size, int vertexCount, int indiceCount)
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
            return Normals.Length * sizeof(byte) +
                   Indices.Length * sizeof(uint) +
                   Marshal.SizeOf<Vector3>() +
                   sizeof(float) +
                   Marshal.SizeOf<Vector3>();
        }

        public void Reuse()
        {
            ArrayPool<byte>.Shared.Return(normals);
            ArrayPool<uint>.Shared.Return(indices);
        }
    }
}