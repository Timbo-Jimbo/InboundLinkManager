using System;

namespace TimboJimbo.InboundLinkManager
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class InboundLinkAssociatedDomain : Attribute
    {
        public readonly string AssociatedDomain;

        public InboundLinkAssociatedDomain(string associatedDomain)
        {
            AssociatedDomain = associatedDomain;
        }
    }
}