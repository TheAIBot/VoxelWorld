using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;

namespace VoxelWorld
{
    internal sealed class TimeNumberAverage<T> where T : INumber<T>
    {
        private readonly ConcurrentQueue<(DateTime Time, T Value)> _timeSamples = new ConcurrentQueue<(DateTime Time, T Value)>();
        private readonly TimeSpan _timeToAverage;

        public TimeNumberAverage(TimeSpan timeToAverage)
        {
            _timeToAverage = timeToAverage;
        }

        public void AddSampleNow(T sample)
        {
            (DateTime Time, T Value) timedSample = (DateTime.Now, sample);
            _timeSamples.Enqueue(timedSample);

            if (!_timeSamples.TryPeek(out var oldestSample))
            {
                return;
            }
            while (timedSample.Time - oldestSample.Time > _timeToAverage)
            {
                _timeSamples.TryDequeue(out var _);

                if (!_timeSamples.TryPeek(out oldestSample))
                {
                    return;
                }
            }
        }

        public float GetAveragePerTimeUnit(TimeSpan timeUnit)
        {
            if (_timeSamples.Count < 2)
            {
                return 0;
            }

            TimeSpan sampleTimeSpan = _timeSamples.Last().Time - _timeSamples.First().Time;
            float timeUnitRatio = timeUnit.Ticks / (float)sampleTimeSpan.Ticks;
            return _timeSamples.Sum(x => Convert.ToSingle(x.Value)) * timeUnitRatio;
        }
    }
}
