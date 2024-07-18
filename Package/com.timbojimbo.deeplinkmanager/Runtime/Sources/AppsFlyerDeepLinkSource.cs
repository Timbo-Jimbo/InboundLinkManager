#if APPSFLYER_AVAILABLE
using System;
using System.Collections.Generic;
using System.Linq;
using AppsFlyerSDK;
using UnityEngine;

namespace TimboJimbo.DeepLinkManager.Sources
{
    public class AppsFlyerDeepLinkSource : IDeepLinkSource
    {
        public event Action<string> OnDeepLinkActivated;
        private NamedLogger _logger = new (nameof(AppsFlyerDeepLinkSource));
        
        public void Activate()
        {
            AppsFlyer.OnDeepLinkReceived += OnAppsFlyerDeepLinkReceived;
            Application.deepLinkActivated += UnityAppDeepLinkActivated;
        }

        private void UnityAppDeepLinkActivated(string obj)
        {
            //if AppsFlyer is not initialized or stopped, we can
            //assume go ahead and process deep links from Unity
            //(otherwise, we'd be doubling up on them...!)
            if(AppsFlyer.instance == null || AppsFlyer.isSDKStopped())
            {
                OnDeepLinkActivated?.Invoke(obj);
            }
        }

        private void OnAppsFlyerDeepLinkReceived(object sender, EventArgs args)
        {
            var deepLinkEventArgs = args as DeepLinkEventsArgs;

            if (deepLinkEventArgs == null)
            {
                _logger.Error("deepLinkEventArgs is null");
                return;
            }
            
            if(deepLinkEventArgs.deepLink == null)
            {
                _logger.Error("deepLinkParamsDictionary is null");
                return;
            }
            
            _logger.Log("DeepLink Received triggered by AppsFlyer SDK, with status: " + deepLinkEventArgs.status);
                    
            var fields = new List<(string path, string value)>();
            _logger.Log("Extracting fields..");
            ExtractFields("Root", deepLinkEventArgs.deepLink, fields);
                    
            _logger.Log("Fields extracted: " + string.Join(", ", fields.Select(f => $"{f.path}={f.value}")));
            var possibleLinkFields = fields.Where(f => f.path.EndsWith(".link")).ToList();
            _logger.Log("Possible links: " + string.Join(", ", possibleLinkFields.Select(f => f.path)));
                    
            //shortest is probably what we want...?
            var linkField = possibleLinkFields.OrderBy(v => v.path.Length).FirstOrDefault();
            _logger.Log("Entry chosen to be treated as deep link: " + linkField.path);
            
            OnDeepLinkActivated?.Invoke(linkField.value);

            void ExtractFields(string path, Dictionary<string, object> dic, List<(string path, string value)> output)
            {
                foreach (var kv in dic)
                {
                    if (kv.Value is Dictionary<string, object> subDic)
                    {
                        ExtractFields($"{path}.{kv.Key}", subDic, output);
                    }
                    else
                    {
                        output.Add(($"{path}.{kv.Key}", kv.Value.ToString()));
                    }
                }
            }
        }

        public void Deactivate()
        {
            AppsFlyer.OnDeepLinkReceived -= OnAppsFlyerDeepLinkReceived;
            Application.deepLinkActivated -= UnityAppDeepLinkActivated;
        }
    }
}
#endif