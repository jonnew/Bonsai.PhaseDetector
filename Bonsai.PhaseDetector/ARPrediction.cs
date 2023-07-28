using OpenCV.Net;
using System;
using System.Collections.Generic;

namespace Bonsai.PhaseDetector
{

    public class ForwardARPrediction
    {
        public ARPrediction.ARModel Model;
        public int Sample0;
        public double[] Prediction;
    }

    public static class ARPrediction
    {
        public struct ARModel
        {
            public double Sigma { get; set; } // GWN SD
            public double[] Coefficients { get; set; } // Coefficients
            //public double[] Reflection { get; set; } // Reflection coefficients
        }

        private static double[] AutoCorrelation(IList<double> timeSeries, int order) 
        {
            if (timeSeries.Count == 0) throw new ArgumentException("Time series cannot be empty.", nameof(timeSeries));

            var r = new double[order + 1];
            var n = timeSeries.Count - 1;

            for (int i = 0; i <= order; i++) // Lags
            {
                for(int j = 0; j <= n - i; j++)
                    r[i] += timeSeries[j] * timeSeries[i + j];
            }

            return r;
        }


        // Verified
        private static ARModel EstimateAR(IList<double> r)
        {
            // TODO: might need more than 1 element
            if (r.Count == 0) throw new ArgumentException("Autocorrelation cannot be empty.", nameof(r));

            var m = r.Count - 1;
            var a = new double[m + 1];
            //var k = new double[r.Count + 1];

            var e = r[0];
            a[0] = 1.0;

            for (int k = 0; k < m; k++)
            {

                // Compute lambda
                double lambda = 0;

                for (int j = 0; j <= k; j++)
                {
                    lambda -= a[j] * r[k + 1 - j];
                }
                lambda /= e;

                // Update a[k]
                for (int n = 0; n <= (k + 1)/2; n++)
                {
                    var tmp = a[k + 1 - n] + lambda * a[n];
                    a[n]= a[n] + lambda * a[k + 1 - n];
                    a[k + 1 - n ] = tmp;
                }

                // Update e
                e *= (1.0 - lambda * lambda);
            }

            return new ARModel
            {
                Sigma = Math.Sqrt(e),
                Coefficients = a,
            };

        }


        public static ForwardARPrediction Predict(IList<double> timeSeries, int order, int forwardSamples, int timeSeries0)
        {

            var model = EstimateAR(AutoCorrelation(timeSeries, order));
            var m = model.Coefficients.Length - 1; // skip coeff 1.0

            var pred = new double[forwardSamples + timeSeries.Count];
            for (int i = 0; i < timeSeries.Count; i++)
                pred[i] = timeSeries[i];

            for (int i = timeSeries.Count; i < pred.Length; i++)
                for (int j = 0; j < m; j++)
                    pred[i] -= model.Coefficients[j + 1] * pred[i - 1 - j];

            return new ForwardARPrediction { Model = model, Sample0 = timeSeries0, Prediction = pred };
        }


        //public static double[] Predict(IList<double> timeSeries, int order, int forwardSteps)
        //{

        //    var model = EstimateAR(AutoCorrelation(timeSeries, order));
        //    var m = model.Coefficients.Length - 1; // skip coeff 1.0

        //    var pred = new double[forwardSteps + m];
        //    for (int i = 0; i < m; i++)
        //        pred[i] = timeSeries[timeSeries.Count - m + i];

        //    for (int i = m; i < pred.Length; i++)
        //        for (int j = 0; j < m; j++)
        //            pred[i] -= model.Coefficients[j+1] * pred[i - 1 - j];

        //    return pred;
        //}


        //public static ARModel EstimateAR(IList<double> r)
        //{
        //    // TODO: might need more than 1 element
        //    if (r.Count == 0) throw new ArgumentException("Autocorrelation cannot be empty.", nameof(r));

        //    var a = new double[r.Count + 1];
        //    var k = new double[r.Count + 1];
        //    var e = r[0];

        //    for (int i = 1; i < r.Count; i++)
        //    {

        //        double v;

        //        if (e == 0.0)
        //            v = 0.0;
        //        else
        //        {
        //            v = -r[i];
        //            for (int j = 1; j < i; j++)
        //                v -= a[j - 1] * r[i - j];
        //            v /= e;

        //            if (v > 1.0 || v < -1.0)
        //            {
        //                Console.Error.WriteLine("Unstable filter k[{0}]={1}", i, v);
        //            }
        //        }

        //        k[i - 1] = v;

        //        // Update prediction coefficients
        //        var jmax = (i - 1) / 2;
        //        for (int j = 1; j <= jmax; j++)
        //        {
        //            var tmp = a[j - 1];
        //            a[j - 1] += v * a[(i - j) - 1];
        //            a[(i - j) - 1] += v * tmp;
        //        }

        //        if ((i - 1) % 2 != 0)
        //        {
        //            a[i / 2 - 1] += v * a[i / 2 - 1];
        //        }
                   
        //        a[i - 1] = v;
        //        e *= (1.0 - v * v);
        //    }

        //    return new ARModel { 
        //        Sigma = Math.Sqrt(e),
        //        Coefficients = a,
        //        Reflection  = k
        //    };

        //}

    }
}
