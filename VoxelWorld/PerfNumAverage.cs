using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelWorld
{
    internal sealed class PerfNumAverage<T>
    {
        private readonly Queue<T> Samples;
        private int SamplesSum;
        private Func<T, int> HowToGetValue;
        private readonly int SamplesNeeded;

        public PerfNumAverage(int samplesNeeded, Func<T, int> sumOfWhat)
        {
            this.Samples = new Queue<T>();
            this.SamplesSum = 0;
            this.HowToGetValue = sumOfWhat;
            this.SamplesNeeded = samplesNeeded;
        }

        public void AddSample(T sample)
        {
            Samples.Enqueue(sample);
            SamplesSum += HowToGetValue(sample);
            if (Samples.Count > SamplesNeeded)
            {
                SamplesSum -= HowToGetValue(Samples.Dequeue());
            }
        }

        public float GetAverage()
        {
            return ((float)SamplesSum) / Samples.Count;
        }

        public bool FilledWithSamples()
        {
            return Samples.Count == SamplesNeeded;
        }

        public bool IsEmpty()
        {
            return Samples.Count == 0;
        }

        public void PrintHistogram()
        {
            int xValues = 30;
            int yValues = 60;
            int xDiv = (Samples.Select(HowToGetValue).Max() + xValues - 1) / xValues;
            int[] histSums = new int[xValues];
            foreach (int sample in Samples.Select(HowToGetValue))
            {
                histSums[sample / xDiv]++;
            }
            int maxValue = histSums.Max();
            for (int i = 0; i < histSums.Length; i++)
            {
                int padding = maxValue == 0 ? 0 : (int)((histSums[i] / (float)maxValue) * yValues);
                Console.WriteLine($"{(i * xDiv).ToString().PadLeft(4, ' ')}|{"".PadLeft(padding, '#')}");
            }
        }
    }
}
