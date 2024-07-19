using System;

namespace TimboJimbo.InboundLinkManager.Sources
{
    public interface IInboundLinkSource
    {
        public void Activate();
        public event Action<string> OnInboundLinkReceived;
        public void Deactivate();
    }
}