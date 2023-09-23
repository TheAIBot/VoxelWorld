using System;
using System.Collections.Concurrent;
using System.Linq;

namespace VoxelWorld
{
    internal sealed class TimeNumberAverage<T>
    {
        private readonly ConcurrentQueue<(DateTime Time, int Value)> _timeSamples = new ConcurrentQueue<(DateTime Time, int Value)>();
        private readonly TimeSpan _timeToAverage;
        private readonly Func<T, int> _howToGetValue;

        public TimeNumberAverage(TimeSpan timeToAverage, Func<T, int> howToGetSample)
        {
            _timeToAverage = timeToAverage;
            _howToGetValue = howToGetSample;
        }

        public void AddSampleNow(T sample)
        {
            (DateTime Time, int Value) timedSample = (DateTime.Now, _howToGetValue(sample));
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
            return _timeSamples.Sum(x => (float)x.Value) * timeUnitRatio;
        }
    }
}
