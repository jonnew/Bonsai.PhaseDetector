using System;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Drawing.Design;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    [Description("Generate a delay, in microseconds, between the current time and a desired phase in a periodic signal using a forward AR prediction.")]
    public class GeneratePhaseLockedStimDelay : Transform<ForwardARPrediction, double> // TODO: Combinator<ForwardARPrediction, int>
    {
        [Range(0, 1e7)] 
        public double PowerThreshold { get; set; }

        [Range(0, Hilbert.Tau)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Precision(2, Math.PI/4)]
        public double StimPhase { get; set; }

        [Range(0, 10000)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        int FixedDelayMicroSeconds { get; set;} =  2000; // Microseconds

        public override IObservable<double> Process(IObservable<ForwardARPrediction> source)
        {
            return source
                .Where(pred => pred.Power > PowerThreshold) // Only process if power is adequate
                .Select(pred =>
                {
                    var ts = new Complex[pred.Prediction.Length];
                    for (int i = 0; i < ts.Length; i++)
                        ts[i] = pred.Prediction[i]; // Math.Sin(i * 2 * Math.PI * 128 / input.Length);

                    var ampPhase = Hilbert.InstaneousAmplitudeAndPhase(ts);

                    // What is the phase at time 0?
                    var phase0 = ampPhase.Phase[pred.Sample0] % Hilbert.Tau;

                    // What is is the unwrapped 0 deg equivalent for the the cycle that occurs at time 0?
                    var start = Hilbert.Tau * Math.Floor(ampPhase.Phase[pred.Sample0] / Hilbert.Tau);

                    // If the phase at time 0 is greater than our target, we need to shift our target phase by one cycle
                    var stimPhase = start + StimPhase; //  phase0 < StimPhase ? start + StimPhase : start + StimPhase + Hilbert.Tau;

                    for (var i = pred.Sample0; i < ampPhase.Phase.Length; i++)
                    {
                        if (ampPhase.Phase[i] > stimPhase)
                        {
                            var delay = 1e6 * pred.Ts * ((i - pred.Sample0) - 0.5) - FixedDelayMicroSeconds;
                            if (delay >= 0)
                            {
                                // Good to go
                                return delay;
                            } else
                            {
                                // Stimulation would have been too late
                                stimPhase += Hilbert.Tau;
                            }
                        }
                    }

                    // Reject in next step
                    return -1d;

                })
                .Where(x => x >= 0);
        }
    }
}
