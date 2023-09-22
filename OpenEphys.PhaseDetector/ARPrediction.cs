using OpenCV.Net;
using System;
using System.Collections.Generic;

namespace OpenEphys.PhaseDetector
{
    public class ForwardARPrediction
    {
        // The auto-regressive model fit to the data
        public ARPrediction.ARModel Model { get; internal set; }

        // The index of the first foward prediction in the Prediction vector
        public int Sample0 { get; internal set; }

        // The signal power of data used to create the Model
        public double Power { get; internal set; }

        // Sample period of prediction
        public double Ts { get; internal set; }

        // Data and forward prediction. Forward prediction starts at index Sample0
        public double[] Prediction { get; internal set; }
    }

    public static class ARPrediction
    {
        public struct ARModel
        {
            public double Sigma { get; set; } // GWN SD
            public double[] Coefficients { get; set; } // Coefficients
            //public double[] Reflection { get; set; } // Reflection coefficients
        }

        private static double[] DeMean(IList<double> timeSeries)
        {
            if (timeSeries.Count == 0) throw new ArgumentException("Time series cannot be empty.", nameof(timeSeries));

            var res = new double[timeSeries.Count];

            // Remove mean
            var mean = 0d;
            foreach (var s in timeSeries) mean += s;

            mean /= timeSeries.Count;

            for (int i = 0; i < timeSeries.Count - 1; i++)
                res[i] = timeSeries[i] - mean;

            return res;
        }

        // TODO: This method causes an artifact where the correlation is reduced for greater lags simply
        // because the amount of data that goes into the sum is reduced
        private static double[] AutoCorrelation(IList<double> timeSeries, int order) 
        {
            if (timeSeries.Count == 0) throw new ArgumentException("Time series cannot be empty.", nameof(timeSeries));

            var r = new double[order + 1];
            var n = timeSeries.Count - 1;

            // Autocovariance
            for (int i = 0; i <= order; i++) // Lags
            {
                // Adust for finite samples
                //var w = (double)n / (n - i);

                for (int j = 0; j <= n - i; j++)
                    r[i] += timeSeries[j] * timeSeries[i + j]; // * w;
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

        public static ForwardARPrediction Predict(IList<double> timeSeries, int order, int windowStart, int windowLength, int forwardSamples)
        {
            var model = EstimateAR(AutoCorrelation(new ArraySegment<double>(DeMean(timeSeries), windowStart, windowLength), order));
            var m = model.Coefficients.Length - 1; // skip coeff 1.0

            var pred = new double[forwardSamples + timeSeries.Count];

            var power = 0d;
            var endIndex = windowStart + windowLength;

            // Use the window for calculating signal power and filling in the first samples of the prediction
            for (int i = windowStart; i < endIndex; i++)
            {
                power += Math.Pow(timeSeries[i], 2);
                pred[i] = timeSeries[i];
            }

            // Energy -> power
            power /= timeSeries.Count;

            // Prediction starts at end of window
            for (int i = endIndex; i < pred.Length; i++)
                for (int j = 0; j < m; j++)
                    pred[i] -= model.Coefficients[j + 1] * pred[i - 1 - j];

            return new ForwardARPrediction { Model = model, Sample0 = timeSeries.Count - 1, Prediction = pred , Power = power };
        }
    }
}
