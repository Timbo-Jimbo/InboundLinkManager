using System;

namespace TimboJimbo.InboundLinkManager
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class InboundLinkCustomScheme : Attribute
    {
        public readonly string Scheme;

        public InboundLinkCustomScheme(string scheme)
        {
            Scheme = scheme;
        }
    }
}