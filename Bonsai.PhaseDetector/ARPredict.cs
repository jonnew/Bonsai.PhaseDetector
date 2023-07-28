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


    public class ARPredict : Transform<Mat, ForwardARPrediction>
    {

        [Description("Autoregressive model order.")]
        [Range(2, int.MaxValue)]
        public int Order { get; set; } = 4;

        [Description("Fraction of data to use to create forward prediction.")]
        [Range(0.5, 0.9)]
        public double Window { get; set; } = 0.75;

        public override IObservable<ForwardARPrediction> Process(IObservable<Mat> source)
        {
            return source.Select(m =>
            {

                if ((m.Cols > 1 && m.Rows > 1) || m.Depth != Depth.F64)
                {
                    throw new WorkflowRuntimeException("This node can only process a 1D vector of doubles.");
                }

                var numel = m.Cols * m.Rows; // Full time series length
                int len = (int)(numel * Window); // Length of window
                int start = (numel - len) / 2; // Start sample of window

                // Cut out a Percent-sized window right in the middle of the the original data
                var orig = m.GetSubRect(new Rect(start, 0, len, 1));
                var data = new double[len];
                Marshal.Copy(orig.Data, data, 0, len);

                // Forward predict 2 * start samples from the end of the window
                // Most current real-time is the middle sample
                return ARPrediction.Predict(data, Order, start * 3, len - 1); // Predict 2 *start samples into the future

            });
        }
    }
}
