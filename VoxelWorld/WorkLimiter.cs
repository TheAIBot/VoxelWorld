using System;
using System.Threading.Tasks.Dataflow;

namespace VoxelWorld
{
    internal static class WorkLimiter
    {
        private static readonly ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 6 };
        private static readonly ActionBlock<Action> DoWork = new ActionBlock<Action>(x => x.Invoke(), options);

        public static void QueueWork(Action work)
        {
            DoWork.Post(work);
        }
    }
}
