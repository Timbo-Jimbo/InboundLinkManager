using System;
using UnityEngine;

namespace TimboJimbo.DeepLinkManager
{
    internal class NamedLogger
    {
        public string Name;
        
        public NamedLogger(string name)
        {
            Name = name;
        }
        
        [HideInCallstack]
        public void LogRuntimeOnly(string message)
        {
            if (Application.isPlaying)
                Log(message);
        }
        
        [HideInCallstack]
        public void Log(string message)
        {
            Debug.Log($"{Name}: {message}");
        }
        
        [HideInCallstack]
        public void Error(string message)
        {
            Debug.LogError($"{Name}: {message}");
        }
        
        [HideInCallstack]
        public void Exception(Exception e)
        {
            Debug.LogException(e);
        }
        
    }
}