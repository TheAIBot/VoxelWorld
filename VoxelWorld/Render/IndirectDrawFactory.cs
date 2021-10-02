using OpenGL.Constructs;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelWorld
{
    internal class IndirectDrawFactory
    {
        private readonly PerfNumAverage<int> AvgVertexCount;
        private readonly PerfNumAverage<int> AvgIndiceCount;
        private readonly int GeometryPerBuffer;

        public IndirectDrawFactory(int geometryPerBuffer)
        {
            GeometryPerBuffer = geometryPerBuffer;

            const int SampleCount = 10_000;
            AvgVertexCount = new PerfNumAverage<int>(SampleCount, x => x);
            AvgIndiceCount = new PerfNumAverage<int>(SampleCount, x => x);
        }

        public void AddGeometrySample(GeometryData geometry)
        {
            AvgVertexCount.AddSample(geometry.Vertices.Length);
            AvgIndiceCount.AddSample(geometry.Indices.Length);
        }

        public IndirectDraw CreateIndirectDraw()
        {
            if (AvgVertexCount.IsEmpty())
            {
                return new IndirectDraw(20_000, 100_000, GeometryPerBuffer);
            }
            else
            {
                int vertexCount = (int)(AvgVertexCount.GetAverage() * GeometryPerBuffer);
                int indiceCount = (int)(AvgIndiceCount.GetAverage() * GeometryPerBuffer);
                return new IndirectDraw(vertexCount, indiceCount, GeometryPerBuffer);
            }
        }

        public bool HasAcceptableBufferSizes(IndirectDraw draw)
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
