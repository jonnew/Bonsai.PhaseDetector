using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using OpenCV.Net;

namespace Bonsai.PhaseDetector
{
    public class ARPredict : Transform<Mat, ForwardARPrediction>
    {
        [Description("Autoregressive model order.")]
        [Range(2, int.MaxValue)]
        public int Order { get; set; } = 10;

        [Description("Fraction of data to use to create forward waveform prediction.")]
        [Range(0.5, 0.9)]
        public double Window { get; set; } = 0.75;

        [Description("Fraction of data to forward predict. 1.0 indicates that " +
            "forward prediction should end at most recent sample. 2.0 indicates that " +
            "prediction should extend to 2x length of the data window.")]
        [Range(1.0, 10)]
        public double ForwardWindow { get; set; } = 0.75;

        [Description("The sampling rate of data to be processed.")]
        [Range(0, 10e6)]
        public double SampleRate { get; set; } = 30193.236714975847;

        [Description("Decimated sample rate. Incoming data is reduced to this rate prior to processing. " +
            "0 indicates that data should not be decimated. Note that data must must not have spectral " +
            "content about the Nyquist cuttoff or aliasing will occur.")]
        [Range(0, 10e6)]
        public double DecimatedSamplingRateHz { get; set; } = 1000;

        public override IObservable<ForwardARPrediction> Process(IObservable<Mat> source)
        {
            var sampleRate = SampleRate;
            var decRate = DecimatedSamplingRateHz;

            return source.Select(m =>
            {

                if ((m.Cols > 1 && m.Rows > 1) || m.Depth != Depth.F64)
                {
                    throw new WorkflowRuntimeException("This node can only process a 1D vector of doubles.");
                }

                if (decRate > 0) // Decimate
                {
                    int total = m.Cols * m.Rows;
                    int every = (int)(sampleRate / decRate);
                    int numel = total / every;
                    int len = (int)(numel * Window); // Length of window
                    int start = (numel - len) / 2; // Start sample of window
                    var fowardSamples = (int)(numel * ForwardWindow - numel);

                    // Convert to Array
                    var full = new double[total];
                    Marshal.Copy(m.Data, full, 0, total);

                    var ts = new double[numel];
                    for (int i = 0, j = 0; j < numel; i += every, j++)
                        ts[j] = full[i];

                    // Predict fowardSamples into the future
                    var ar = ARPrediction.Predict(ts, Order, start, len, fowardSamples);
                    ar.Ts = 1 / decRate;
                    return ar;
                }
                else
                {
                    var numel = m.Cols * m.Rows; // Full time series length
                    int len = (int)(numel * Window); // Length of window
                    int start = (numel - len) / 2; // Start sample of window
                    var fowardSamples = (int)(numel * ForwardWindow - numel);

                    // Convert to Array
                    var ts = new double[numel];
                    Marshal.Copy(m.Data, ts, 0, numel);

                    // Predict fowardSamples into the future
                    var ar = ARPrediction.Predict(ts, Order, start, len, fowardSamples);
                    ar.Ts = 1 / sampleRate;
                    return ar;

                }
            });
        }
    }
}
