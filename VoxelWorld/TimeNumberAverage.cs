using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelWorld
{
    internal sealed class TimeNumberAverage<T>
    {
        private readonly Queue<(DateTime Time, int Value)> _timeSamples = new Queue<(DateTime Time, int Value)>();
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

            while (timedSample.Time - _timeSamples.Peek().Time > _timeToAverage)
            {
                _timeSamples.Dequeue();
            }
        }

        public float GetAveragePerTimeUnit(TimeSpan timeUnit)
        {
            if (_timeSamples.Count < 2)
            {
                return 0;
            }

            TimeSpan sampleTimeSpan = _timeSamples.First().Time - _timeSamples.Last().Time;
            float timeUnitRatio = timeUnit.Ticks / (float)_timeToAverage.Ticks;
            return _timeSamples.Average(x => (float)x.Value) * timeUnitRatio;
        }
    }
}
