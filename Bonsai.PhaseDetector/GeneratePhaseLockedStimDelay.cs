using System;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Drawing.Design;

namespace Bonsai.PhaseDetector
{

    public class GeneratePhaseLockedStimDelay : Transform<ForwardARPrediction, double> // TODO: Combinator<ForwardARPrediction, int>
    {
        // TODO??
        //const double FixedDelay = 2000d; // Microseconds

        [Range(0, 1e7)] 
        public double PowerThreshold { get; set; }

        [Range(0, Hilbert.Tau)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Precision(2, Math.PI/4)]
        public double StimPhase { get; set; }

        //[Description("Sample rate of ")]
        //public double SampleRateHz { get; set; }

        public override IObservable<double> Process(IObservable<ForwardARPrediction> source)
        {
            //var uSecPerSample = 1e6 / SampleRateHz;

            return source
                .Where(pred => pred.Power > PowerThreshold) // Only process if power is adquate
                .Select(pred =>
                {
                    var ts = new Complex[pred.Prediction.Length];
                    for (int i = 0; i < ts.Length; i++)
                        ts[i] = pred.Prediction[i]; // Math.Sin(i * 2 * Math.PI * 128 / input.Length);

                    var ampPhase = Hilbert.InstaneousAmplitudeAndPhase(ts);

                    // What is the phase at time 0?
                    var phase0 = ampPhase.Phase[pred.Sample0] % Hilbert.Tau;

                    // What is 0 deg in the cycle that occurs at time 0?
                    var start = Hilbert.Tau * Math.Floor(ampPhase.Phase[pred.Sample0] / Hilbert.Tau);

                    // If the phase at time 0 is greater than our target, we need to shift our target by one cycle
                    double stimPhase = phase0 < StimPhase ? start + StimPhase : start + StimPhase + Hilbert.Tau;

                    for (var i = pred.Sample0; i < ampPhase.Phase.Length; i++)
                    {
                        if (ampPhase.Phase[i] > stimPhase)
                            return 1e6 * pred.Ts * ((i - pred.Sample0) - 0.5); 
                    }

                    return 0d;

                });
        }
    }
}
