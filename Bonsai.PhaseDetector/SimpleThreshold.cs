using System;
using System.Linq;
using System.Numerics;
using OpenCV.Net;
using System.Reactive.Linq;
using FFTW.NET;
using System.ComponentModel;
using System.Drawing.Design;
using System.Collections.Generic;

namespace Bonsai.PhaseDetector
{

    public class SimpleThreshold : Combinator<Mat, int>
    {
        private const double Tau = 2 * Math.PI;

        public int Channel { get; set; }    

        public int Threshold { get; set; }  
        
        public int TimeoutSamples { get; set; }



        private int timeout;

        public override IObservable<int> Process(IObservable<Mat> source)
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
            .Select(m => 0);

        }         
    }
}
