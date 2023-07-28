using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai.Dsp;
using OpenCV.Net;
using System.Numerics;
using System.Xml.Serialization;

namespace Bonsai.PhaseDetector
{
    [Description("A zero-phase Butterworth bandpass filter.")]
    internal class ZeroPhaseButterworthFilter
    {

        readonly double[] A; // Feedback coefficients
        readonly double[] B; // Feedforward coefficients
        readonly double[] Z; // Initial conditions
        readonly int EdgeNumber;
        readonly int N;

        public ZeroPhaseButterworthFilter(double fs, double low, double high, int order)
        {
            // Create filter coefficients
            var butter = FilterDesign.ButterworthPrototype(order);
            FilterDesign.GetFilterCoefficients(butter, new[] { low / fs, high / fs }, FilterType.BandPass, out B, out A);

            // Pre-compute initial conditions
            N = A.Length - 1;

            // K and Z do not change if A and B dont, so I think this should be optimized out as part of a constrcutor
            var K = new double[N, N];
            var z = new double[N];

            for (int i = 0; i < N; i++)
            {

                z[i] = B[i + 1] - B[0] * A[i + 1];
                K[i, 0] = A[i + 1];

                for (int j = 1; j <= i + 1; j++)
                {
                    if (j == i)
                    {
                        K[i, j] = 1.0;
                    }
                    else if (j < N && j == i + 1)
                    {
                        K[i, j] = -1.0;
                    }
                }
            }

            K[0, 0] += 1.0;

            var KMat = Mat.CreateMatHeader(K);
            var IC = Mat.CreateMatHeader(z, N, 1, Depth.F64, 1);

            CV.Invert(KMat, KMat, InversionMethod.Svd);
            CV.MatMul(KMat, IC, IC); // IC slightly different than matlab impl

            Z = new double[N];
            for (int i = 0; i < N; i++)
            {
                Z[i] = IC[i].Val0;
            }

            EdgeNumber = 3 * N;
        }

        public double[] FiltFilt(double[] data)
        {

            var dl = data.Length;
            var x0_2 = 2 * data[0];
            var xf_2 = 2 * data[dl - 1];
            var Xi = new double[EdgeNumber];
            var Xf = new double[EdgeNumber];

            for (int i = 0; i < EdgeNumber; i++)
            {
                Xi[i] = x0_2 - data[EdgeNumber - i];
                Xf[i] = xf_2 - data[dl - 2 - i];
            }

            var ICi = new double[N];
            var ICf = new double[N];

            for (int i = 0; i < N; i++)
            {
                ICi[i] = Z[i] * Xi[0];
            }

            FilterForward(B, A, Xi, ref ICi);
            var Ys = FilterForward(B, A, data, ref ICi);
            var Yf = FilterForward(B, A, Xf, ref ICi);


            for (int i = 0; i < N; i++)
            {
                ICf[i] = Z[i] * Yf[EdgeNumber - 1];
            }

            FilterReverse(B, A, Yf, ref ICf);
            return FilterReverse(B, A, Ys, ref ICf);

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
