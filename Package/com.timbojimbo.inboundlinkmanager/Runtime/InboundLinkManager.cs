using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TimboJimbo.InboundLinkManager.Handlers;
using TimboJimbo.InboundLinkManager.Sources;
using UnityEditor;
using UnityEngine;

namespace TimboJimbo.InboundLinkManager
{
    public static class InboundLinkManager
    {
        public static List<InboundLinkData> UnhandledInboundLinks { get; } = new();
        
        internal static readonly Dictionary<string, Type> Parsers = new();
        internal static readonly List<string> AssociatedDomains = new();
        internal static readonly List<string> CustomSchemes = new();
     
        private static NamedLogger _logger = new NamedLogger(nameof(InboundLinkManager));
        private static List<IInboundLinkHandler> _activeHandlers = new();
        private static IInboundLinkSource _inboundLinkSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        #endif
        private static void InitOnLoad()
        {
            _logger.LogRuntimeOnly("Initialising");
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttributes(typeof(InboundLinkParserAttribute), true).FirstOrDefault();

                    if (attribute is InboundLinkParserAttribute dlpAttribute)
                    {
                        //ensure type has a ctor that takes a string
                        if (type.GetConstructor(new[] { typeof(string) }) == null)
                        {
                            _logger.Error($"Classes decorated with {nameof(InboundLinkParserAttribute)} require a constructor that takes a string (the inbound link string). Your class {type} is missing this constructor.");
                            continue;
                        }

                        //ensure type inherits from InboundLinkData
                        if (!typeof(InboundLinkData).IsAssignableFrom(type))
                        {
                            _logger.Error($"Classes decorated with {nameof(InboundLinkParserAttribute)} must inherit from {nameof(InboundLinkData)}. Your class {type} does not.");
                            continue;
                        }
                        
                        Parsers[dlpAttribute.Path] = type;
                    }
                }
                
                var customSchemeAttributes = assembly.GetCustomAttributes(typeof(InboundLinkCustomScheme), true).Cast<InboundLinkCustomScheme>().ToList();
                foreach (var customSchemeAttribute in customSchemeAttributes)
                {
                    if (CustomSchemes.Contains(customSchemeAttribute.Scheme)) continue;
                    CustomSchemes.Add(customSchemeAttribute.Scheme);
                }
                
                var associatedDomainAttributes = assembly.GetCustomAttributes(typeof(InboundLinkAssociatedDomain), true).Cast<InboundLinkAssociatedDomain>().ToList();
                foreach (var entry in associatedDomainAttributes)
                {
                    if (AssociatedDomains.Contains(entry.AssociatedDomain)) continue;
                    AssociatedDomains.Add(entry.AssociatedDomain);
                }
            }
            
            foreach (var associatedDomain in AssociatedDomains)
                _logger.LogRuntimeOnly($"Registered Associated Domain: {associatedDomain}");
            
            foreach (var scheme in CustomSchemes)
                _logger.LogRuntimeOnly($"Registered Custom Scheme: {scheme}");
            
            foreach (var (match, dataType) in Parsers)
                _logger.LogRuntimeOnly($"Registered Parser: {match} -> {dataType}");

            if (_inboundLinkSource == null)
            {
                _logger.LogRuntimeOnly($"Initializing default inbound link source");
                ResetInboundLinkSource();
            }

            // Cold start and Application.absoluteURL not null? We should handle it...!
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                _logger.LogRuntimeOnly($"'Application.absoluteURL' was populated, processing it as an inbound link...");
                OnInboundLinkReceived(Application.absoluteURL);
            }
        }
 
        private static void OnInboundLinkReceived(string link)
        {
            _logger.LogRuntimeOnly($"Inbound link received: {link}");
            
            if(!TryHandleInboundLink(link))
                _logger.LogRuntimeOnly($"Could not parse link: {link}");
        }
        
        public static bool CanHandleInboundLink(string url)
        {
            foreach (var customScheme in CustomSchemes)
            {
                if(url.StartsWith(customScheme))
                    return true;
            }
            
            foreach (var host in AssociatedDomains)
            {
                if(url.StartsWith($"{Uri.UriSchemeHttp}://{host}") || url.StartsWith($"{Uri.UriSchemeHttps}://{host}"))
                    return true;
            }

            if (Application.isEditor)
            {
                if(url.StartsWith($"{Uri.UriSchemeHttp}://localhost") || url.StartsWith($"{Uri.UriSchemeHttps}://localhost"))
                    return true;
            }
            
            return false;
        }

        public static bool TryHandleInboundLink(string link)
        {
            if(!CanHandleInboundLink(link))
                return false;
            
            link = link.TrimEnd('/');

            foreach (var (match, dataType) in Parsers)
            {
                if(link.Contains(match))
                {
                    _logger.LogRuntimeOnly($"Parsing Inbound Link: {link} -> {dataType}");
                    var inboundLinkData = (InboundLinkData) Activator.CreateInstance(dataType, HttpUtility.UrlDecode(link));
                    var handled = false;
                    
                    for (var i = _activeHandlers.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            var result = _activeHandlers[i].HandleInboundLink(inboundLinkData);
                            
                            if (result == Result.Handled)
                            {
                                _logger.LogRuntimeOnly($"Inbound Link Data was handled by {_activeHandlers[i].GetType()}");
                                handled = true;
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Exception(new InboundLinkManagerException($"Exception thrown while handing Inbound Link: {link} -> {dataType}", e));
                        }
                    }
                    
                    if (!handled)
                    {
                        _logger.LogRuntimeOnly($"Inbound Link Data not handled by any active handler, adding to queue.");
                        UnhandledInboundLinks.Add(inboundLinkData);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }
        
        public static void AddHandler(IInboundLinkHandler handler, bool processQueued)
        {
            _activeHandlers.Add(handler);
            
            if (processQueued)
            {
                for (var i = UnhandledInboundLinks.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var result = handler.HandleInboundLink(UnhandledInboundLinks[i]);
                    
                        if (result == Result.Handled)
                        {
                            _logger.LogRuntimeOnly($"Inbound Link Data handled by {handler.GetType()}");
                            UnhandledInboundLinks.RemoveAt(i);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Exception(new InboundLinkManagerException($"Exception thrown while handling Inbound Link: {UnhandledInboundLinks[i]}", e));
                    }
                }
            }
        }
        
        public static void RemoveHandler(IInboundLinkHandler consumer)
        {
            _activeHandlers.Remove(consumer);
        }
        
        public static IDisposable AddHandlerScope(IInboundLinkHandler consumer, bool processAnyUnconsumed)
        {
            AddHandler(consumer, processAnyUnconsumed);
            return new CbOnDispose(() => RemoveHandler(consumer));
        }
        
        public static void SetInboundLinkSource(IInboundLinkSource inboundLinkSource)
        {
            if (_inboundLinkSource != null)
            {
                _inboundLinkSource.Deactivate();
                _inboundLinkSource.OnInboundLinkReceived -= OnInboundLinkReceived;
                _inboundLinkSource = null;
            }

            _inboundLinkSource = inboundLinkSource;
            _inboundLinkSource.Activate();
            _inboundLinkSource.OnInboundLinkReceived += OnInboundLinkReceived;
            
            _logger.LogRuntimeOnly($"Set InboundLinkSource to {inboundLinkSource.GetType()}");
        }

        public static void ResetInboundLinkSource()
        {
            IInboundLinkSource defaultInboundLinkSource =
#if APPSFLYER_UNITY && !UNITY_EDITOR
                new AppsFlyerDeepLinkSource();
#else
                new UnityApplicationInboundLinkSource();
#endif
            
            SetInboundLinkSource(defaultInboundLinkSource);
        }
        
        private class CbOnDispose : IDisposable
        {
            private readonly Action _cb;
            public CbOnDispose(Action cb) => _cb = cb;
            public void Dispose() => _cb();
        }
        
        private class InboundLinkManagerException : Exception
        {
            public InboundLinkManagerException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}