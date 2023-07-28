using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCV.Net;

namespace Bonsai.PhaseDetector
{
    public class ButterworthArrayFiltFilt : Transform<double[], double[]>
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
        [Range(2, 10)]
        public int Order { get; set; } = 4;



        public override IObservable<double[]> Process(IObservable<double[]> source)
        {

            if (HighCutoff < LowCutoff)
            {
                throw new WorkflowRuntimeException("HighCutoff must be greater than or equal to LowCutoff.");
            }

            var filter = new ZeroPhaseButterworthFilter(SampleRate, LowCutoff, HighCutoff, Order);

            return source.Select(v =>
            {
                return filter.FiltFilt(v);
            });
        }
    }
}
