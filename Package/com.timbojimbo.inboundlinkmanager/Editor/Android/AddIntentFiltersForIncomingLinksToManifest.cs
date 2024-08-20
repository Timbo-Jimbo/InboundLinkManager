#if UNITY_ANDROID
using System;
using System.Linq;
using System.Xml;
using UnityEditor.Android;

namespace TimboJimbo.InboundLinkManager.Editor.Android
{
    internal class AddIntentFiltersForIncomingLinksToManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100000;
        
        private readonly NamedLogger _logger = new ($"{nameof(InboundLinkManager)}[Manifest Patcher]");
        
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            const string androidPrefix = "android";
            const string androidXmlNamespace = "http://schemas.android.com/apk/res/android";
            
            var manifestPath = path + "/src/main/AndroidManifest.xml";

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            {
                //find activity with the launch intent
                var nsMgr = new XmlNamespaceManager(new NameTable());
                nsMgr.AddNamespace(androidPrefix, androidXmlNamespace);
                var mainActivity = doc.SelectSingleNode("/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN']", nsMgr);
                
                if(mainActivity == null)
                {
                    _logger.Error("Unable to add intent filters to AndroidManifest.xml for App Links/Deep Links - No activity with the MAIN intent filter was found.");
                    return;
                }

                DeletePreviouslyGeneratedIntentFilters();
                
                //add the intent filters
                var inboundLinkPrefixes = InboundLinkManager.Parsers
                    .Select(x => x.Key.TrimEnd('/'))
                    .Distinct()
                    .ToArray();

                {
                    var invalidPrefixes = inboundLinkPrefixes
                        .Where(x => !x.StartsWith('/'))
                        .ToArray();

                    if (invalidPrefixes.Any())
                    {
                        var invalidPrefixList = string.Join(", ", invalidPrefixes);
                        throw new Exception($"Detected invalid inbound link prefixes ({invalidPrefixList}). Ensure all 'InboundLinkParser' path's start with a '/'. Please fix the 'path' in your InboundLinkParserAttribute(s).");
                    }
                }

                var associatedDomains = InboundLinkManager.AssociatedDomains;
        
                var allHostPrefixCombos = associatedDomains
                    .SelectMany(host => inboundLinkPrefixes.Select(prefix => (host, prefix)))
                    .ToArray();
        
                //handle only links with specific host and path prefixes for http/https
                //aka 'app links' on android
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
                        "App Links",
                        new[]
                        {
                            Uri.UriSchemeHttp,
                            Uri.UriSchemeHttps
                        }, 
                        allHostPrefixCombos
                    );
                }

                //handle all links for custom schemes for this app 
                //aka 'deep links' on android
                if (InboundLinkManager.CustomSchemes.Any())
                {
                    // (we want *all* 'your-scheme://' links to be handled by the app...
                    // ...so we don't need to pass in a list of hosts + prefixes to filter for)
                    // ie for the following:
                    // schemes: ["your-scheme"]
                    // resulting intent filters will handle:
                    // your-scheme://*
                    AddIntentFilterForIncomingLinks(
                        "Deep Links",
                        InboundLinkManager.CustomSchemes.ToArray(), 
                        Array.Empty<(string, string)>()
                    );
                }

                XmlAttribute CreateAndroidAttribute(string key, string value)
                {
                    XmlAttribute attr = doc.CreateAttribute(androidPrefix, key, androidXmlNamespace);
                    attr.Value = value;
                    return attr;
                }

                void DeletePreviouslyGeneratedIntentFilters()
                {
                    _logger.Log($"Clearing previously generated intent filters");

                    var intentFilters = mainActivity.SelectNodes("intent-filter[@android:name='inbound-link-manager-intent-filter-root']", nsMgr);
                    
                    if (intentFilters != null)
                    {
                        foreach (XmlElement intentFilter in intentFilters)
                        {
                            if(intentFilter.PreviousSibling?.NodeType == XmlNodeType.Comment)
                                mainActivity.RemoveChild(intentFilter.PreviousSibling);
                            mainActivity.RemoveChild(intentFilter);
                        }   
                    }
                }

                void AddIntentFilterForIncomingLinks(string sectionName, string[] schemes, (string host, string pathPrefix)[] hostPrefixCombos) 
                {
                    var intentFilter = doc.CreateElement("intent-filter");
                    intentFilter.Attributes.Append(CreateAndroidAttribute("name", "inbound-link-manager-intent-filter-root"));
                    intentFilter.Attributes.Append(CreateAndroidAttribute("autoVerify", "true"));
                    
                    var action = doc.CreateElement("action");
                    action.Attributes.Append(CreateAndroidAttribute("name", "android.intent.action.VIEW"));
                    intentFilter.AppendChild(action);

                    var browsableCategory = doc.CreateElement("category");
                    browsableCategory.Attributes.Append(CreateAndroidAttribute("name", "android.intent.category.BROWSABLE"));
                    intentFilter.AppendChild(browsableCategory);
                    
                    var defaultCategory = doc.CreateElement("category");
                    defaultCategory.Attributes.Append(CreateAndroidAttribute("name", "android.intent.category.DEFAULT"));
                    intentFilter.AppendChild(defaultCategory);
                    
                    foreach (var scheme in schemes)
                    {
                        var data = doc.CreateElement("data");
                        data.Attributes.Append(CreateAndroidAttribute("scheme", scheme));
                        intentFilter.AppendChild(data);
                        
                        _logger.Log($"Added intent filter for incoming links with scheme '{scheme}'");
                    }

                    foreach (var (host, pathPrefix) in hostPrefixCombos)
                    {
                        var data = doc.CreateElement("data");
                        data.Attributes.Append(CreateAndroidAttribute("host", host));
                        data.Attributes.Append(CreateAndroidAttribute("pathPrefix", pathPrefix));
                        intentFilter.AppendChild(data);
                        
                        _logger.Log($"Added intent filter for incoming links from host '{host}' with path prefix '{pathPrefix}'");
                    }
                    
                    mainActivity.AppendChild(intentFilter);
                    mainActivity.InsertBefore(doc.CreateComment($" {nameof(InboundLinkManager)}: Injected {sectionName} "), intentFilter);
                }
            }
            
            doc.Save(manifestPath);
        }
    }
}
#endif