using System;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;

namespace OpenEphys.PhaseDetector
{
    public struct AmpPhase
    {
        public double[] Amplitude { get; internal set; }
        public double[] Phase { get; internal set; }

    }

    internal static class Hilbert
    {
        public const double Tau = 2 * Math.PI;

        internal static AmpPhase InstaneousAmplitudeAndPhase(double[] timeSeries)
        {

            var signal = new Complex32[timeSeries.Length];

            for (int i = 0; i < timeSeries.Length; i++)
                signal[i] = new Complex32((float)timeSeries[i], 0);

            return InstaneousAmplitudeAndPhase(signal);
        }

        internal static AmpPhase InstaneousAmplitudeAndPhase(Complex32[] timeSeries)
        {
            var numel = timeSeries.Length;
           // var output = new Complex[numel];
            var amplitude = new double[numel];
            var phase = new double[numel];


            Fourier.Forward(timeSeries);

            // Create analytical signal in frequency domain
            for (int i = 1; i < numel / 2; i++)
            {
                timeSeries[i] *= 2;
            }

            for (int i = numel / 2 + 1; i < numel; i++)
            {
                timeSeries[i] = 0;
            }

            Fourier.Inverse(timeSeries);

            // Extract phase
            var lastPhase = timeSeries[0].Phase;
            double offset = 0;

            // TODO: Feels very inefficient
            for (int i = 0; i < numel; i++)
            {
                amplitude[i] = Math.Sqrt(timeSeries[i].Magnitude);

                var dPhase = timeSeries[i].Phase - lastPhase;
                if (Math.Abs(dPhase) > Math.PI)
                {
                    offset += dPhase > 0 ? -Tau : Tau;
                }

                phase[i] = offset + timeSeries[i].Phase;
                lastPhase = timeSeries[i].Phase;
            }


            return new AmpPhase { Amplitude = amplitude, Phase = phase };
        }

    }
}
