using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using OpenCV.Net;

namespace Bonsai.PhaseDetector
{
    public class ButterworthFiltFilt : Transform<Mat, Mat>
    {
        [Description("The sampling rate of data to be processed.")]
        [Range(0, 10e6)]
        public double SampleRate { get; set; } = 30193.236714975847;

        [Description("The high-frequency cut-off of the filter in units of SampleRate.")]
        [Range(0, 10e6)]
        public double HighCutoff { get; set; } = 13;

        [Description("The low-frequency cut-off of the filter in units of SampleRate.")]
        [Range(0, 10e6)]
        public double LowCutoff { get; set; } = 7;

        [Description("Filter order.")]
        [Range(2, int.MaxValue)]
        public int Order { get; set; } = 2;

        // TODO: filter mutliple channels
        // TODO: work with other Depths?
        public override IObservable<Mat> Process(IObservable<Mat> source)
        {

            if (HighCutoff < LowCutoff)
            {
                throw new WorkflowRuntimeException("HighCutoff must be greater than or equal to LowCutoff.");
            }

            var filter = new ZeroPhaseButterworthFilter(SampleRate, LowCutoff, HighCutoff, Order);
            var sampleRate = SampleRate;

            return source.Select(m =>
            {
                if ((m.Cols > 1 && m.Rows > 1) || m.Depth != Depth.F64)
                {
                    throw new WorkflowRuntimeException("This node can only process a 1D vector of doubles.");
                }

                var numel = m.Cols * m.Rows;

                var data = new double[numel];
                Marshal.Copy(m.Data, data, 0, numel);
                var filtered = filter.FiltFilt(data);

                return Mat.CreateMatHeader(filtered, 1, numel, m.Depth, m.Channels);

            });
        }
    }
}
