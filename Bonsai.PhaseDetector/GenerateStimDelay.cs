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
    public class PhaseModel
    {
        public double M { get; internal set; }
        public double B { get; internal set; }
        public double AvgEnv { get; internal set; }
        public ulong StimTime { get; internal set; }

        public int NextStim { get; internal set; }  
    }

    public class GenerateStimDelay : Combinator<Tuple<Mat, Mat>, PhaseModel>
    {
        private const double Tau = 2 * Math.PI;

        [Range(20, 100)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        public double WindowPercent { get; set; } = 80;

        public double AverageEnvelopeThreshold { get; set; }

        [Range(0, 2 * Math.PI)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        public double StimPhase { get; set; }

        private ulong currentSample;

        public override IObservable<PhaseModel> Process(IObservable<Tuple<Mat, Mat>> source)
        {
            currentSample = 0;

            return source
                .Select(ampPhase =>
                {
                    var amp = ampPhase.Item1;
                    var phase = ampPhase.Item2;

                    // Vectors Only
                    if ((amp.Cols > 1 && amp.Rows > 1) || amp.Channels > 1 ||
                        (phase.Cols > 1 && phase.Rows > 1) || phase.Channels > 1)
                    {
                        throw new WorkflowRuntimeException("This node can only process a tuple contain 2 1D vector of doubles.");
                    }

                    var numel = amp.Cols * amp.Rows;

                    // Check for multiple of 2
                    if (numel != phase.Cols * phase.Rows)
                    {
                        throw new WorkflowRuntimeException("Tuple must contain vectors of equal length.");
                    }

                    // First step, check average envelope in window
                    var width = (int)(numel * 0.01 * WindowPercent);
                    var start = (numel - width) >> 1;


                    var phaseWindow = phase.GetSubRect(new Rect(start, 0, width, 1));
                    //var phaseEst = new Mat(2, phaseWindow.Cols, phaseWindow.Depth, 1);
                    var phasePoints = new List<Point2d>();

                    for (var i = 0; i < phaseWindow.Cols; i++)
                    {
                        phasePoints.Add(new Point2d(i + start, phaseWindow[i].Val0));

                    }

                    currentSample += 1000;

                    return new Tuple<Mat, List<Point2d>, int>(amp.GetSubRect(new Rect(start, 0, width, 1)), phasePoints, numel);
                })
                .Where(window =>
                {
                    return CV.Avg(window.Item1).Val0 > AverageEnvelopeThreshold;
                })
                .Select(window =>
                {
                    var points = window.Item2;

                    int numPoints = points.Count;
                    double meanX = points.Average(point => point.X);
                    double meanY = points.Average(point => point.Y);

                    double sumXSquared = points.Sum(point => point.X * point.X);
                    double sumXY = points.Sum(point => point.X * point.Y);

                    var m = (sumXY / numPoints - meanX * meanY) / (sumXSquared / numPoints - meanX * meanX);
                    var b = (meanY - m * meanX) + window.Item3;

                    var bEnd = (m * window.Item3 + b) % (Tau);


                    var nextStim = 0;
                    //var nextStim = StimPhase > bEnd ? (int)((StimPhase - bEnd) / m) : (int)((StimPhase + Tau - bEnd) / m);

                    return new PhaseModel { B = b, M = m, AvgEnv = CV.Avg(window.Item1).Val0, StimTime = currentSample + (ulong)nextStim, NextStim = nextStim };
                });
        }
    }
}
