using System;
using System.Linq;
using System.Reactive.Linq;
using System.ComponentModel;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    [Description("Calculate the running Z-score of sequence of doubles.")]
    public class MeanStd : Transform<double, (double Mean, double Std)>
    {
        public override IObservable<(double Mean, double Std)> Process(IObservable<double> source)
        {
            ulong count = 0;
            double mean = 0;
            double m2 = 0;

            return source.Select(s =>
            {
                count++;
                var d = s - mean;
                mean += d / count;
                var d2 = s - mean;
                m2 += d * d2;
                return count == 1 ? (0, 1) : (mean, Math.Sqrt(m2 / (count - 1)));

            });
        }
    }
}
