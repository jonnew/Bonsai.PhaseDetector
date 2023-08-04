using System;
using System.Linq;
using OpenCV.Net;
using System.Reactive.Linq;

namespace Bonsai.PhaseDetector
{
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
