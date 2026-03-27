using System.Collections.Generic;
using System.Threading;

namespace BK7231Flasher
{
    public class XR806Flasher : XRRomFlasherBase
    {
        static readonly Dictionary<uint, string> SECTION_NAMES = new Dictionary<uint, string>
        {
            { 0xA5FF5A00, "boot"     },
            { 0xA5FE5A01, "app"      },
            { 0xA5FD5A02, "app_xip"  },
            { 0xA5FA5A05, "wlan_bl"  },
            { 0xA5F95A06, "wlan_fw"  },
            { 0xA5F85A07, "wlan_sdd" },
        };

        public XR806Flasher(CancellationToken ct) : base(ct)
        {
        }

        protected override Dictionary<uint, string> SectionNames
        {
            get { return SECTION_NAMES; }
        }

        protected override string LegacyLoaderUnsupportedMessage
        {
            get { return "This XR806 transport expects the ROM-resident flash commands seen on observed XR806 BROMs (for example v4)."; }
        }
    }
}
