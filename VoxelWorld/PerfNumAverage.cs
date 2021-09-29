using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelWorld
{
    internal class PerfNumAverage<T>
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
            int[] histogram = new int[Samples.Select(HowToGetValue).Max() + 1];
            int usablesamples = 0;
            foreach (int sample in Samples.Select(HowToGetValue))
            {
                if (sample < histogram.Length)
                {
                    histogram[sample]++;
                    usablesamples++;
                }
            }

            int xValues = 30;
            int yValues = 60;

            int xDiv = histogram.Length / xValues;

            int[] histSums = new int[xValues];
            for (int i = 0; i < histSums.Length; i++)
            {
                histSums[i] = histogram.AsSpan(i * xDiv, xDiv).ToArray().Sum();

            }
            int maxValue = histSums.Max();
            for (int i = 1; i < histSums.Length; i++)
            {
                Console.WriteLine($"{(i * xDiv).ToString().PadLeft(3, ' ')}|{"".PadLeft((int)(((float)histSums[i]) / (((float)maxValue) / yValues)), '#')}");
            }
        }
    }
}
