using System;
using System.Numerics;
using OpenCV.Net;
using FFTW.NET;
using Bonsai;

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

        internal static AmpPhase InstaneousAmplitudeAndPhase(Mat timeSeries)
        {
            // Vectors Only
            if ((timeSeries.Cols > 1 && timeSeries.Rows > 1) || timeSeries.Channels > 1) // m.Depth != Depth.F64 || 
            {
                throw new WorkflowRuntimeException("This node can only process a 1D vector of doubles.");
            }

            var numel = timeSeries.Cols * timeSeries.Rows;

            // Check for multiple of 2
            if (numel % 2 != 0)
            {
                throw new WorkflowRuntimeException("Vector length must be a multiple of 2.");
            }

            var ts = new Complex[numel];

            for (int i = 0; i < ts.Length; i++)
                ts[i] = timeSeries[i].Val0;

            return InstaneousAmplitudeAndPhase(ts);
        }

        internal static AmpPhase InstaneousAmplitudeAndPhase(Complex[] timeSeries)
        {
            var numel = timeSeries.Length;
            var output = new Complex[numel];
            var amplitude = new double[numel];
            var phase = new double[numel];

            using (var pinIn = new PinnedArray<Complex>(timeSeries))
            using (var pinOut = new PinnedArray<Complex>(output))
            {
                // TODO: Seems to be a bug that prevents pinIn from being a pinned array of doubles even though DFT.FFT supports it
                // Convert to freq. domain
                DFT.FFT(pinIn, pinOut);

                // Create analytical signal in frequency domain
                for (int i = 1; i < numel / 2; i++)
                {
                    output[i] *= 2;
                }

                for (int i = numel / 2 + 1; i < numel; i++)
                {
                    output[i] = 0;
                }

                // Back to time domain
                DFT.IFFT(pinOut, pinOut);

                // Extract phase
                var lastPhase = output[0].Phase;
                double offset = 0;

                // TODO: Feels very inefficient
                for (int i = 0; i < numel; i++)
                {
                    amplitude[i] = Math.Sqrt(output[i].Magnitude);

                    //phase[i] = output[i].Phase;

                    var dPhase = output[i].Phase - lastPhase;
                    if (Math.Abs(dPhase) > Math.PI)
                    {
                        offset += dPhase > 0 ? -Tau : Tau;
                    }

                    phase[i] = offset + output[i].Phase;
                    lastPhase = output[i].Phase;
                }
            }

            return new AmpPhase { Amplitude = amplitude, Phase = phase };
        }
    }
}
