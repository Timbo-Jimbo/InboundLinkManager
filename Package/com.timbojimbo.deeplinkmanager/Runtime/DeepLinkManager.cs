using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TimboJimbo.DeepLinkManager.Handlers;
using TimboJimbo.DeepLinkManager.Sources;
using UnityEditor;
using UnityEngine;

namespace TimboJimbo.DeepLinkManager
{
    public static class DeepLinkManager
    {
        public static List<DeepLinkData> UnhandledDeepLinkDataQueue { get; } = new();
        
        internal static readonly Dictionary<string, Type> Parsers = new();
        internal static readonly List<string> Hosts = new();
        internal static readonly List<string> CustomSchemas = new();
     
        private static NamedLogger _logger = new NamedLogger(nameof(DeepLinkManager));
        private static List<IDeepLinkHandler> _activeHandlers = new();
        private static IDeepLinkSource _deepLinkSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        #endif
        private static void InitOnLoad()
        {
            _logger.LogRuntimeOnly("Initialising DeepLinkManager");
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttributes(typeof(DeepLinkParserAttribute), true).FirstOrDefault();

                    if (attribute is DeepLinkParserAttribute dlpAttribute)
                    {
                        //ensure type has a ctor that takes a string
                        if (type.GetConstructor(new[] { typeof(string) }) == null)
                        {
                            _logger.Error($"Classes decorated with {nameof(DeepLinkParserAttribute)} require a constructor that takes a string (the deeplink url). Your class {type} is missing this constructor.");
                            continue;
                        }

                        //ensure type inherits from DeepLinkData
                        if (!typeof(DeepLinkData).IsAssignableFrom(type))
                        {
                            _logger.Error($"Classes decorated with {nameof(DeepLinkParserAttribute)} must inherit from {nameof(DeepLinkData)}. Your class {type} does not.");
                            continue;
                        }
                        
                        Parsers[dlpAttribute.Path] = type;
                    }
                }
                
                var customSchemaAttributes = assembly.GetCustomAttributes(typeof(DeepLinkCustomSchemaAttribute), true).Cast<DeepLinkCustomSchemaAttribute>().ToList();
                foreach (var customSchemaAttribute in customSchemaAttributes)
                {
                    if (CustomSchemas.Contains(customSchemaAttribute.Schema)) continue;
                    CustomSchemas.Add(customSchemaAttribute.Schema);
                }
                
                var deepLinkHostAttributes = assembly.GetCustomAttributes(typeof(DeepLinkHostAttribute), true).Cast<DeepLinkHostAttribute>().ToList();
                foreach (var entry in deepLinkHostAttributes)
                {
                    if (Hosts.Contains(entry.Host)) continue;
                    Hosts.Add(entry.Host);
                }
            }
            
            foreach (var host in Hosts)
                _logger.LogRuntimeOnly($"Registered Deep Link Host: {host}");
            
            foreach (var schema in CustomSchemas)
                _logger.LogRuntimeOnly($"Registered Deep Link Custom Schema: {schema}");
            
            foreach (var (match, dataType) in Parsers)
                _logger.LogRuntimeOnly($"Registered Deep Link Parser: {match} -> {dataType}");

            if (_deepLinkSource == null)
            {
                _logger.LogRuntimeOnly($"Initializing default DeepLink source");
                ClearDeepLinkSource();
            }

            // Cold start and Application.absoluteURL not null so process Deep Link.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                _logger.LogRuntimeOnly($"'Application.absoluteURL' was populated, processing it as a deeplink...");
                OnDeepLinkActivated(Application.absoluteURL);
            }
        }
 
        private static void OnDeepLinkActivated(string url)
        {
            _logger.LogRuntimeOnly($"Deep link activated: {url}");
            
            if(!TryRaiseDeepLinkEvent(url))
                _logger.LogRuntimeOnly($"Could not parse Deep Link: {url}");
        }
        
        public static bool IsDeepLinkUrl(string url)
        {
            foreach (var customSchema in CustomSchemas)
            {
                if(url.StartsWith(customSchema))
                    return true;
            }
            
            foreach (var host in Hosts)
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

        public static bool TryRaiseDeepLinkEvent(string url)
        {
            if(!IsDeepLinkUrl(url))
                return false;
            
            url = url.TrimEnd('/');

            foreach (var (match, dataType) in Parsers)
            {
                if(url.Contains(match))
                {
                    _logger.LogRuntimeOnly($"Parsing Deep Link: {url} -> {dataType}");
                    var deepLinkData = (DeepLinkData) Activator.CreateInstance(dataType, HttpUtility.UrlDecode(url));
                    var handled = false;
                    
                    for (var i = _activeHandlers.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            var result = _activeHandlers[i].HandleDeepLinkData(deepLinkData);
                            
                            if (result == Result.Handled)
                            {
                                _logger.LogRuntimeOnly($"Deep Link Data handled by {_activeHandlers[i].GetType()}");
                                handled = true;
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Exception(new DeepLinkManagerException($"DeepLinkManager.TryRaiseDeepLinkEventDirectly: Exception thrown while handing Deep Link: {url} -> {dataType}", e));
                        }
                    }
                    
                    if (!handled)
                    {
                        _logger.LogRuntimeOnly($"Deep Link Data not handled by any active handler, adding to queue.");
                        UnhandledDeepLinkDataQueue.Add(deepLinkData);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }
        
        public static void AddHandler(IDeepLinkHandler handler, bool processQueued)
        {
            _activeHandlers.Add(handler);
            
            if (processQueued)
            {
                for (var i = UnhandledDeepLinkDataQueue.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var result = handler.HandleDeepLinkData(UnhandledDeepLinkDataQueue[i]);
                    
                        if (result == Result.Handled)
                        {
                            _logger.LogRuntimeOnly($"Deep Link Data handled by {handler.GetType()}");
                            UnhandledDeepLinkDataQueue.RemoveAt(i);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Exception(new DeepLinkManagerException($"DeepLinkManager.AddConsumer: Exception thrown while handling Deep Link: {UnhandledDeepLinkDataQueue[i]}", e));
                    }
                }
            }
        }
        
        public static void RemoveHandler(IDeepLinkHandler consumer)
        {
            _activeHandlers.Remove(consumer);
        }
        
        public static IDisposable AddHandlerScope(IDeepLinkHandler consumer, bool processAnyUnconsumed)
        {
            AddHandler(consumer, processAnyUnconsumed);
            return new CbOnDispose(() => RemoveHandler(consumer));
        }
        
        public static void SetDeepLinkSource(IDeepLinkSource deepLinkSource)
        {
            if (_deepLinkSource != null)
            {
                _deepLinkSource.Deactivate();
                _deepLinkSource.OnDeepLinkActivated -= OnDeepLinkActivated;
                _deepLinkSource = null;
            }

            _deepLinkSource = deepLinkSource;
            _deepLinkSource.Activate();
            _deepLinkSource.OnDeepLinkActivated += OnDeepLinkActivated;
            
            _logger.LogRuntimeOnly($"Set DeepLinkSource to {deepLinkSource.GetType()}");
        }

        public static void ClearDeepLinkSource()
        {
            IDeepLinkSource defaultDeepLinkSource =
#if APPSFLYER_UNITY && !UNITY_EDITOR
                new AppsFlyerDeepLinkSource();
#else
                new UnityApplicationDeepLinkSource();
#endif
            
            SetDeepLinkSource(defaultDeepLinkSource);
        }
        
        private class CbOnDispose : IDisposable
        {
            private readonly Action _cb;
            public CbOnDispose(Action cb) => _cb = cb;
            public void Dispose() => _cb();
        }
        
        private class DeepLinkManagerException : Exception
        {
            public DeepLinkManagerException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}