using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenCV.Net;
using System.Reactive.Linq;
using FFTW.NET;

namespace Bonsai.PhaseDetector
{
    public class InstantenousAmpPhase : Transform<Mat, Tuple<Mat, Mat>>
    {
        private const double Tau = 2 * Math.PI;

        public override IObservable<Tuple<Mat, Mat>> Process(IObservable<Mat> source)
        {

            return source.Select(m =>
            {

                // Vectors Only
                if ((m.Cols > 1 && m.Rows > 1) || m.Channels > 1) // m.Depth != Depth.F64 || 
                {
                    throw new WorkflowRuntimeException("This node can only process a 1D vector of doubles.");
                }
                
                var numel = m.Cols * m.Rows;

                // Check for multiple of 2
                if (numel % 2 != 0)
                {
                    throw new WorkflowRuntimeException("Vector length must be a multiple of 2.");
                }

                var input = new Complex[numel];
                var output = new Complex[numel];
                var amplitude = new double[numel];
                var phase = new double[numel];
                //var ampPhase = new double[2, numel];

                for (int i = 0; i < input.Length; i++)
                    input[i] = m[i].Val0; // Math.Sin(i * 2 * Math.PI * 128 / input.Length);

                using (var pinIn = new PinnedArray<Complex>(input))
                using (var pinOut = new PinnedArray<Complex>(output))
                {
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

                //var data = new double[numel];
                //Marshal.Copy(m.Data, data, 0, numel);
                return new Tuple<Mat, Mat>(Mat.CreateMatHeader(amplitude), Mat.CreateMatHeader(phase)); //, m.Rows, 2 * m.Cols, m.Depth, 1); // ;
            });
        }
    }
}
