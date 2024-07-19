using System;
using UnityEngine.Scripting;

namespace TimboJimbo.InboundLinkManager
{
    [AttributeUsage(AttributeTargets.Class)]
    public class InboundLinkParserAttribute : PreserveAttribute
    {
        public readonly string Path;

        public InboundLinkParserAttribute(string path)
        {
            Path = path;
        }
    }
}