using System;
using UnityEngine;

namespace TimboJimbo.DeepLinkManager.Sources
{
    public class UnityApplicationDeepLinkSource : IDeepLinkSource
    {
        public event Action<string> OnDeepLinkActivated;
        
        public void Activate()
        {
            Application.deepLinkActivated += UnityAppDeepLinkActivated;
        }


        private void UnityAppDeepLinkActivated(string obj)
        {
            OnDeepLinkActivated?.Invoke(obj);
        }

        public void Deactivate()
        {
            Application.deepLinkActivated -= UnityAppDeepLinkActivated;
        }
    }
    
}