using OpenGL;
using System.Runtime.CompilerServices;

namespace VoxelWorld
{
    internal class GpuTimer
    {
        private readonly uint[] QueryIndex = new uint[1];
        private readonly long[] GpuTime = new long[1];

        public GpuTimer()
        {
            Gl.GenQueries(QueryIndex.Length, QueryIndex);
        }

        public void StartTimer()
        {
            Gl.BeginQuery(QueryTarget.TimeElapsed, QueryIndex[0]);
        }

        public void StopTimer()
        {
            Gl.EndQuery(QueryTarget.TimeElapsed);
        }

        public long GetTimeInNS()
        {
            Gl.GetQueryObjecti64v(QueryIndex[0], GetQueryObjectParam.QueryResult, GpuTime);
            return GpuTime[0];
        }

        public long GetTimeInMS()
        {
            const long nanoToMsRatio = 1_000_000;
            return GetTimeInNS() / nanoToMsRatio;
        }
    }
}
