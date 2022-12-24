using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BK7231Flasher
{
    internal static class SecurityProtocolTypeExtensions
    {
        public const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        public const SecurityProtocolType Tls11 = (SecurityProtocolType)768;
    }
}
