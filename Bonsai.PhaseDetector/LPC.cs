using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using OpenCV.Net;

namespace Bonsai.PhaseDetector
{
    public static class LPC
    {
        public struct ARModel
        {
            public double Sigma { get; set; } // GWN SD
            public double[] Coefficients { get; set; } // Coefficients
            public double[] Reflection { get; set; } // Reflection coefficients
        }

        public static double[] AutoCorrelation(IList<double> timeSeries, int order) 
        {
            if (timeSeries.Count == 0) throw new ArgumentException("Time series cannot be empty.", nameof(timeSeries));

            var r = new double[order + 1];

            for (int i = 0; i <= order; i++)
            {
                double b = 0.0;

                for (int j = i; j < timeSeries.Count; j++)
                    b += timeSeries[j] * timeSeries[j - i];

                r[i] = b;

            }

            return r;
        }

        public static ARModel EstimateAR(IList<double> r)
        {
            // TODO: might need more than 1 element
            if (r.Count == 0) throw new ArgumentException("Autocorrelation cannot be empty.", nameof(r));

            var a = new double[r.Count + 1];
            var k = new double[r.Count + 1];
            var e = r[0];

            for (int i = 1; i <= r.Count; i++)
            {

                double v;

                if (e == 0.0)
                    v = 0.0;
                else
                {
                    v = -r[i];
                    for (int j = 1; j < i; j++)
                        v -= a[j - 1] * r[i - j];
                    v /= e;

                    if (v > 1.0 || v < -1.0)
                    {
                        Console.Error.WriteLine("Unstable filter k[{0}]={1}", i, v);
                    }
                }

                k[i - 1] = v;

                // Update prediction coefficients
                var jmax = (i - 1) / 2;
                for (int j = 1; j <= jmax; j++)
                {
                    var tmp = a[j - 1];
                    a[j - 1] += v * a[(i - j) - 1];
                    a[(i - j) - 1] += v * tmp;
                }

                if ((i - 1) % 2 != 0)
                {
                    a[i / 2 - 1] += v * a[i / 2 - 1];
                }
                   
                a[i - 1] = v;
                e *= (1.0 - v * v);
            }

            return new ARModel { 
                Sigma = Math.Sqrt(e),
                Coefficients = a,
                Reflection  = k
            };

        }

        public static double[] FiltFilt(double[] b, double[] a, double[] data)
        {
            if (a.Length != b.Length) throw new ArgumentException("Coefficient arrays must be the same length", nameof(a));

            var n = a.Length - 1;

            // K and Z do not change if A and B dont, so I think this should be optimized out as part of a constrcutor
            var K = new double[n, n];
            var Z = new double[n];

            for (int i = 0; i < n; i++) {

                Z[i] = b[i + 1] - b[0] * a[i + 1];
                K[i, 0] = a[i + 1];

                for (int j = 1; j <= i + 1; j++)
                {
                    if (j == i)
                    {
                        K[i, j] = 1.0;
                    }
                    else if (j < n && j == i + 1)
                    {
                        K[i, j] = -1.0;
                    }
                }
            }

            K[0, 0] += 1.0;

            var KMat = Mat.CreateMatHeader(K);
            var IC = Mat.CreateMatHeader(Z, 8, 1, Depth.F64, 1);

            CV.Invert(KMat, KMat, InversionMethod.Svd);
            CV.MatMul(KMat, IC, IC); // IC slightly different than matlab impl

            var nEdge = 3 * n;
            var dl = data.Length;
            var x0_2 = 2 * data[0];
            var xf_2 = 2 * data[dl - 1];
            var Xi = new double[nEdge];
            var Xf = new double[nEdge];


            for (int i = 0; i < nEdge; i++)
            {
                Xi[i] = x0_2 - data[nEdge - i];
                Xf[i] = xf_2 - data[dl - 2 - i];
            }

            var ICi = new double[n];
            var ICf = new double[n];

            for (int i = 0; i < n; i++)
            {
                ICi[i] = IC[i].Val0 * Xi[0];
            }

            var dum = FilterForward(b, a, Xi, ref ICi);
            var Ys = FilterForward(b, a, data, ref ICi);
            var Yf = FilterForward(b, a, Xf, ref ICi);


            for (int i = 0; i < n; i++)
            {
                ICf[i] = IC[i].Val0 * Yf[nEdge - 1];
            }

            FilterReverse(b, a, Yf, ref ICf);
            return FilterReverse(b, a, Ys, ref ICf);

        }


        internal static double[] FilterForward(double[] b, double[] a, double[] data, ref double[] Z)
        {
            if (a.Length != b.Length) throw new ArgumentException("Coefficient arrays must be the same length", nameof(a));
            var output = new double[data.Length];
            var order = Z.Length;

            for (int i = 0; i < data.Length; i++)  
            {
                var Xi = data[i];                       // Get signal
                var Yi = b[0] * Xi + Z[0];              // Filtered value
                for (int j = 1; j < order; j++)         // Update conditions
                {    
                    Z[j - 1] = b[j] * Xi + Z[j] - a[j] * Yi;
                }
                Z[order - 1] = b[order] * Xi - a[order] * Yi;

                output[i] = Yi;                         // Write to output
            }

            return output;
        }

        internal static double[] FilterReverse(double[] b, double[] a, double[] data, ref double[] Z)
        {
            if (a.Length != b.Length) throw new ArgumentException("Coefficient arrays must be the same length", nameof(a));
            var output = new double[data.Length];
            var order = a.Length - 1;

            for (int i = data.Length - 1; i >= 0; i--)
            {
                var Xi = data[i];                       // Get signal
                var Yi = b[0] * Xi + Z[0];              // Filtered value
                for (int j = 1; j < order; j++)         // Update conditions
                {
                    Z[j - 1] = b[j] * Xi + Z[j] - a[j] * Yi;
                }
                Z[order - 1] = b[order] * Xi - a[order] * Yi;

                output[i] = Yi;                         // Write to output
            }

            return output;
        }
    }
}
