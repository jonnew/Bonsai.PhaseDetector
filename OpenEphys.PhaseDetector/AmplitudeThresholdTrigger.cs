using System;
using System.Linq;
using OpenCV.Net;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    public class AmplitudeThresholdTrigger : Combinator<Mat, double>
    {
        public int Channel { get; set; }    

        public int Threshold { get; set; }  
        
        public int TimeoutSamples { get; set; }

        public double DelayMicroSeconds { get; set; }

        private int timeout;

        public override IObservable<double> Process(IObservable<Mat> source)
        {
            timeout = 0;

            return source
            .Where(m =>
            {
                var row = m.GetRow(Channel);

                if (timeout > 0) {
                    timeout -= row.Cols;
                    return false;
                }

                if (timeout <= 0)
                {
                    for (int i = 0; i < row.Cols; i++)
                    {
                        if (row[i].Val0 > Threshold)
                        {
                            timeout = TimeoutSamples;
                            return true;
                        }

                    }
                }

                return false;
            })
            .Select(m => DelayMicroSeconds);

        }         
    }
}
