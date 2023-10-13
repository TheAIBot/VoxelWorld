using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VoxelWorld.Voxel;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed class IndirectDrawFactory
    {
        internal static PerfNumAverage<int> AvgVertexCount;
        internal static PerfNumAverage<int> AvgIndiceCount;
        private readonly GL _openGl;
        private readonly int GeometryPerBuffer;

        public IndirectDrawFactory(GL openGl, int geometryPerBuffer)
        {
            _openGl = openGl;
            GeometryPerBuffer = geometryPerBuffer;

            const int SampleCount = 10_000;
            AvgVertexCount = new PerfNumAverage<int>(SampleCount, x => x);
            AvgIndiceCount = new PerfNumAverage<int>(SampleCount, x => x);
        }

        public void AddGeometrySample(GeometryData geometry)
        {
            AvgVertexCount.AddSample(geometry.Normals.Length);
            AvgIndiceCount.AddSample(geometry.Indices.Length);
        }

        public MultiBufferedIndirectDraw CreateIndirectDraw()
        {
            const int bufferCount = 3;
            if (AvgVertexCount.IsEmpty())
            {
                return new MultiBufferedIndirectDraw(_openGl, bufferCount, 20_000, 100_000, GeometryPerBuffer);
            }
            else
            {
                int vertexCount = (int)(AvgVertexCount.GetAverage() * GeometryPerBuffer);
                int indiceCount = (int)(AvgIndiceCount.GetAverage() * GeometryPerBuffer);
                return new MultiBufferedIndirectDraw(_openGl, bufferCount, vertexCount, indiceCount, GeometryPerBuffer);
            }
        }

        public bool HasAcceptableBufferSizes(MultiBufferedIndirectDraw draw)
        {
            float expectedVertexCount = AvgVertexCount.GetAverage() * GeometryPerBuffer;
            float expectedIndiceCount = AvgIndiceCount.GetAverage() * GeometryPerBuffer;

            float actualVertexCount = draw.VertexBufferSize();
            float actualIndiceCount = draw.IndiceBufferSize();

            float vertexDiff = MathF.Abs(expectedVertexCount - actualVertexCount);
            float indiceDiff = MathF.Abs(expectedIndiceCount - actualIndiceCount);

            float vertexError = vertexDiff / expectedVertexCount;
            float indiceError = indiceDiff / expectedIndiceCount;

            const float acceptableError = 0.2f;
            return vertexError < acceptableError && indiceError < acceptableError;
        }

        public long GetAverageGridMemUsage()
        {
            return (long)(AvgVertexCount.GetAverage() * Marshal.SizeOf<Vector3>() * 2 +
                AvgIndiceCount.GetAverage() * Marshal.SizeOf<uint>() +
                Marshal.SizeOf<DrawElementsIndirectCommand>());
        }
    }
}
