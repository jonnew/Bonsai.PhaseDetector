using System;
using System.Linq;
using OpenCV.Net;
using System.Reactive.Linq;
using System.ComponentModel;
using Bonsai;

namespace OpenEphys.PhaseDetector
{
    [Description("Calculate the instaneous amplitude (envelope) and phase of an input vector.")]
    public class InstantenousAmpPhase : Transform<Mat, AmpPhase>
    {
        public override IObservable<AmpPhase> Process(IObservable<Mat> source)
        {
            return source.Select(m =>
            {
                return Hilbert.InstaneousAmplitudeAndPhase(m);
            });
        }
    }
}
