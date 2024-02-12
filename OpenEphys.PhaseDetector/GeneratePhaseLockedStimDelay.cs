using System;
using System.Linq;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Drawing.Design;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    public class StimulationTime
    {
        public StimulationTime(ulong totalSamples, double delayMicroseconds, bool valid)
        {
            IsValid = valid;
            TotalSamples = totalSamples;
            DelayMicroseconds = delayMicroseconds;

        }

        internal bool IsValid { get; private set; }

        public double DelayMicroseconds { get; private set; }

        public ulong TotalSamples { get; private set; }

    }

    [Description("Generate a delay, in microseconds, between the current time and a desired phase in a periodic signal using a forward AR prediction.")]
    public class GeneratePhaseLockedStimDelay : Transform<ForwardARPrediction, StimulationTime> // TODO: Combinator<ForwardARPrediction, int>
    {
        [Description("Signal power requried to deliver stimulus. If z-scoring is used, this has units of sigmas.")]
        public double PowerThreshold { get; set; } = 2.0;

        [Description("Power mean and standard devition to be used for z-scoring. " +
            "If left at default, power is not z-scored.")]
        public (double Mean, double Std) PowerMeanStd { get; set; } = (0, 1);

        [Range(0, Hilbert.Tau)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Precision(2, Math.PI/4)]
        [Description("Phase, in radians, to deliver stimulus.")]
        public double StimPhase { get; set; }

        [Range(0, 10000)]
        [Editor(DesignTypes.SliderEditor, typeof(UITypeEditor))]
        [Description("Fixed delay, in microseconds, added to stimulus delivery time to " +
            "account for fixed delay in hardware communication. Generally zero or set empirically.")]
        public int FixedDelayMicroSeconds { get; set;} = 0; // Microseconds

        public override IObservable<StimulationTime> Process(IObservable<ForwardARPrediction> source)
        {
            ulong totalSamples = 0;

            return source
                .Where(pred => {

                    // Total samples processed
                    totalSamples += (ulong)(pred.Sample0 + 1);

                    return (pred.Power - PowerMeanStd.Mean) / PowerMeanStd.Std > PowerThreshold; // Only process if power is adequate
                })
                .Select(pred =>
                {
                    //var ts = new Complex[pred.Prediction.Length];
                    //for (int i = 0; i < ts.Length; i++)
                    //    ts[i] = pred.Prediction[i]; // Math.Sin(i * 2 * Math.PI * 128 / input.Length);

                    var ampPhase = Hilbert.InstaneousAmplitudeAndPhase(pred.Prediction);

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
                            var delay = 1e6 * pred.Ts * (i - pred.Sample0 - 0.5) - FixedDelayMicroSeconds;
                            if (delay >= 0)
                            {
                                // Good to go
                                return new StimulationTime(totalSamples, delay, true);
                            } else
                            {
                                // Stimulation would have been too late
                                stimPhase += Hilbert.Tau;
                            }
                        }
                    }

                    // Reject in next step
                    return new StimulationTime(totalSamples, 0, false);

                })
                .Where(x => x.IsValid);
        }
    }
}
