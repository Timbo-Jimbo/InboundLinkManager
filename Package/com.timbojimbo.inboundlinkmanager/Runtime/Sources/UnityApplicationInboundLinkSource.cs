using System;
using UnityEngine;

namespace TimboJimbo.InboundLinkManager.Sources
{
    public class UnityApplicationInboundLinkSource : IInboundLinkSource
    {
        public event Action<string> OnInboundLinkReceived;
        
        public void Activate()
        {
            Application.deepLinkActivated += UnityAppDeepLinkActivated;
        }


        private void UnityAppDeepLinkActivated(string obj)
        {
            OnInboundLinkReceived?.Invoke(obj);
        }

        public void Deactivate()
        {
            Application.deepLinkActivated -= UnityAppDeepLinkActivated;
        }
    }
    
}