using Silk.NET.OpenGL;

namespace VoxelWorld
{
    internal sealed class GpuTimer
    {
        private readonly uint[] QueryIndex = new uint[1];
        private readonly long[] GpuTime = new long[1];
        private readonly GL _openGl;

        public GpuTimer(GL openGl)
        {
            _openGl = openGl;
            _openGl.GenQueries((uint)QueryIndex.Length, QueryIndex);
        }

        public void StartTimer()
        {
            _openGl.BeginQuery(QueryTarget.TimeElapsed, QueryIndex[0]);
        }

        public void StopTimer()
        {
            _openGl.EndQuery(QueryTarget.TimeElapsed);
        }

        public long GetTimeInNS()
        {
            _openGl.GetQueryObject(QueryIndex[0], QueryObjectParameterName.Result, GpuTime);
            return GpuTime[0];
        }

        public long GetTimeInMS()
        {
            const long nanoToMsRatio = 1_000_000;
            return GetTimeInNS() / nanoToMsRatio;
        }
    }
}
