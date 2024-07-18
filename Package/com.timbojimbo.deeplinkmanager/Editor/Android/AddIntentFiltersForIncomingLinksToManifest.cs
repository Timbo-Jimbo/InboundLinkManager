using System;
using System.Linq;
using System.Xml;

#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace TimboJimbo.DeepLinkManager.Editor.Android
{
    public class AddIntentFiltersForIncomingLinksToManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get; }
        private NamedLogger _logger = new NamedLogger("DeepLinkManager[Manifest Patcher]");
        
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            const string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";
            
            string manifestPath = path + "/src/main/AndroidManifest.xml";
            var doc = new System.Xml.XmlDocument();
            doc.Load(manifestPath);
            {
                //find activity with the launch intent
                var mainActivity = doc.SelectSingleNode("/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN']");
                
                if(mainActivity == null)
                {
                    _logger.Error("Unable to add intent filters to AndroidManifest.xml for DeepLinks - No activity with the MAIN intent filter was found.");
                    return;
                }
                
                //add the intent filters
                var deepLinkPathPrefixes = DeepLinkManager.Parsers
                    .Select(x => x.Key.TrimEnd('/'))
                    .Distinct()
                    .ToArray();
            
                var invalidDeepLinks = deepLinkPathPrefixes
                    .Where(x => !x.StartsWith('/'))
                    .ToArray();

                if (invalidDeepLinks.Any())
                {
                    var invalidDeepLinksString = string.Join(", ", invalidDeepLinks);
                    throw new Exception($"Detected one or more invalid Deep Links ({invalidDeepLinksString}). All Deep Links must start with a '/'. Please fix the 'path' in your DeepLinkTypeAttribute(s).");
                }

                var deepLinkHosts = DeepLinkManager.Hosts;
        
                var allHostPrefixCombos = deepLinkHosts
                    .SelectMany(host => deepLinkPathPrefixes.Select(prefix => (host, prefix)))
                    .ToArray();
        
                //handle only links with specific host and path prefixes for http/https
                if (allHostPrefixCombos.Any())
                {
                    // (we don't want to attempt to handle *every* http/https link...
                    // ...so we use a list of host+prefix combinations to filter for the ones we want to handle)
                    // ie for the following:
                    // hosts: ["www.example.com", "stg.example.com", "dev.example.com"], prefixes: ["/path1", "/path2"]
                    // resulting intent filters will handle:
                    // http://www.example.com/path1, http://www.example.com/path2, http://stg.example.com/path1,
                    // http://stg.example.com/path2, http://dev.example.com/path1, http://dev.example.com/path2
                    // https://www.example.com/path1, https://www.example.com/path2, https://stg.example.com/path1,
                    // https://stg.example.com/path2, https://dev.example.com/path1, https://dev.example.com/path2
                    
                    AddIntentFilterForIncomingLinks(
                        new[]
                        {
                            Uri.UriSchemeHttp,
                            Uri.UriSchemeHttps
                        }, 
                        allHostPrefixCombos
                    );
                }

                //handle all links for custom schemas for this app 
                if (DeepLinkManager.CustomSchemas.Any())
                {
                    // (we want *all* 'your-schema://' links to be handled by the app...
                    // ...so we dont need to pass in a list of hosts + prefixes to filter for)
                    // ie for the following:
                    // schemas: ["your-schema"]
                    // resulting intent filters will handle:
                    // your-schema://*
                    AddIntentFilterForIncomingLinks(
                        DeepLinkManager.CustomSchemas.ToArray(), 
                        Array.Empty<(string, string)>()
                    );
                }

                XmlAttribute CreateAndroidAttribute(string key, string value)
                {
                    XmlAttribute attr = doc.CreateAttribute("android", key, AndroidXmlNamespace);
                    attr.Value = value;
                    return attr;
                }
                
                void AddIntentFilterForIncomingLinks(string[] schemas, (string host, string pathPrefix)[] hostPrefixCombos) 
                {
                    var intentFilter = doc.CreateElement("intent-filter");
                    intentFilter.Attributes?.Append(CreateAndroidAttribute("autoVerify", "true"));
                    
                    var action = doc.CreateElement("action");
                    action.Attributes?.Append(CreateAndroidAttribute("name", "android.intent.action.VIEW"));
                    intentFilter.AppendChild(action);

                    var browsableCategory = doc.CreateElement("category");
                    browsableCategory.Attributes?.Append(CreateAndroidAttribute("name", "android.intent.category.BROWSABLE"));
                    intentFilter.AppendChild(browsableCategory);
                    
                    var defaultCategory = doc.CreateElement("category");
                    defaultCategory.Attributes?.Append(CreateAndroidAttribute("name", "android.intent.category.DEFAULT"));
                    intentFilter.AppendChild(defaultCategory);
                    
                    foreach (var schema in schemas)
                    {
                        var data = doc.CreateElement("data");
                        data.Attributes?.Append(CreateAndroidAttribute("scheme", schema));
                        intentFilter.AppendChild(data);
                        
                        _logger.Log($"Added intent filter for incoming links with schema '{schema}'");
                    }

                    foreach (var (host, pathPrefix) in hostPrefixCombos)
                    {
                        var data = doc.CreateElement("data");
                        data.Attributes?.Append(CreateAndroidAttribute("host", host));
                        data.Attributes?.Append(CreateAndroidAttribute("pathPrefix", pathPrefix));
                        intentFilter.AppendChild(data);
                        
                        _logger.Log($"Added intent filter for incoming links from host '{host}' with path prefix '{pathPrefix}'");
                    }
                    
                    mainActivity.AppendChild(intentFilter);
                }
            }
            doc.Save(manifestPath);
        }
    }
    
#if !UNITY_ANDROID
    
    // This is a dummy interface to allow the code to compile in the editor
    // (just makes it easier to work with when you're not building for Android)
    internal interface IPostGenerateGradleAndroidProject
    {
        void OnPostGenerateGradleAndroidProject(string path);
    }
    
#endif

}
