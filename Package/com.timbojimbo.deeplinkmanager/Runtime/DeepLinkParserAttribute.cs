using System;

namespace TimboJimbo.DeepLinkManager
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DeepLinkParserAttribute : Attribute
    {
        public readonly string Path;

        public DeepLinkParserAttribute(string path)
        {
            Path = path;
        }
    }
}