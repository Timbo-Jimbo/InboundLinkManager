using System;

namespace TimboJimbo.DeepLinkManager
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class DeepLinkCustomSchemeAttribute : Attribute
    {
        public readonly string Scheme;

        public DeepLinkCustomSchemeAttribute(string scheme)
        {
            Scheme = scheme;
        }
    }
}