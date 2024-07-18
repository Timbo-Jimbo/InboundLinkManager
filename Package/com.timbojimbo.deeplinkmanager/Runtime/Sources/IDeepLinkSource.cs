using System;

namespace TimboJimbo.DeepLinkManager.Sources
{
    public interface IDeepLinkSource
    {
        public void Activate();
        public event Action<string> OnDeepLinkActivated;
        public void Deactivate();
    }
}