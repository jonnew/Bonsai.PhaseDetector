using System;
using System.Linq;
using System.Reactive.Linq;
using System.ComponentModel;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    [Description("Calculate the instaneous amplitude (envelope) and phase of an input vector.")]
    public class InstantenousAmpPhase : Transform<double[], AmpPhase>
    {
        public override IObservable<AmpPhase> Process(IObservable<double[]> source)
        {
            return source.Select(s =>
            {
                return Hilbert.InstaneousAmplitudeAndPhase(s);
            });
        }
    }
}
