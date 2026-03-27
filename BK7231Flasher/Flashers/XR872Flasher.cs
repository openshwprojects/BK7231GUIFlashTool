using System.Collections.Generic;
using System.Threading;

namespace BK7231Flasher
{
    public class XR872Flasher : XRRomFlasherBase
    {
        static readonly Dictionary<uint, string> SECTION_NAMES = new Dictionary<uint, string>
        {
        };

        public XR872Flasher(CancellationToken ct) : base(ct)
        {
        }

        protected override Dictionary<uint, string> SectionNames
        {
            get { return SECTION_NAMES; }
        }

        protected override string LegacyLoaderUnsupportedMessage
        {
            get { return "This XR872 transport supports BROM v2 and above."; }
        }
    }
}
