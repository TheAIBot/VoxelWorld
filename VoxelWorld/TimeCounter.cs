using System;
using System.Collections.Generic;

namespace VoxelWorld
{
    internal sealed class TimeCounter
    {
        private readonly Queue<DateTime> Samples = new Queue<DateTime>();
        private readonly TimeSpan TimeSlice;
        private readonly int AverageOverTimeSliceCount;

        public TimeCounter(TimeSpan timeSlice, int averageOverTimeSliceCount)
        {
            TimeSlice = timeSlice;
            AverageOverTimeSliceCount = averageOverTimeSliceCount;
        }

        public void IncrementCounter()
        {
            Samples.Enqueue(DateTime.UtcNow);
            TimeSpan maxStoreTime = TimeSlice * AverageOverTimeSliceCount;
            DateTime oldestSampleToStore = DateTime.UtcNow - maxStoreTime;

            while (Samples.Count > 0 &&
                   Samples.Peek() < oldestSampleToStore)
            {
                Samples.Dequeue();
            }
        }

        public float GetAverage()
        {
            return Samples.Count / AverageOverTimeSliceCount;
        }
    }
}
