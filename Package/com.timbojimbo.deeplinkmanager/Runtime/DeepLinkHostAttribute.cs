using System;

namespace TimboJimbo.DeepLinkManager
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class DeepLinkHostAttribute : Attribute
    {
        public readonly string Host;

        public DeepLinkHostAttribute(string host)
        {
            Host = host;
        }
    }
}