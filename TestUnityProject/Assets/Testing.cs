using System;
using TimboJimbo.DeepLinkManager;
using TimboJimbo.DeepLinkManager.Handlers;
using UnityEngine;

// Register a custom schema!
[assembly: DeepLinkCustomSchema("log-example")]

// This is how you would register a host
// [assembly: DeepLinkHost("www.example.com")] 

[DeepLinkParser("/log/")]
public class LogTextDeepLinkData : DeepLinkData
{
    public string Text;
    
    public LogTextDeepLinkData(string deepLink)
    {
        //ie 'log-example://log/Hello World' (The deep link string has been URL Decoded for you already)
        Text = deepLink.Substring(deepLink.LastIndexOf('/') + 1);
    }
}

public class Testing : MonoBehaviour, IDeepLinkHandler
{
    public void Start()
    {
        DeepLinkManager.AddHandler(this, true);
    }

    public void InjectLogDeepLink(string text)
    {
        //it is not neccecary to create a Uri object as TryRaiseDeepLinkEvent accepts a string
        //but this is just to emulate a real world scenario...!
        //The URL (And by extension the deep link data will be URL Encoded...:
        // 'log-example://log/Hello World' -> 'log-example://log/Hello%20World' 
        //But DeepLinkManager will decode it for you before passing it to your DeepLinkData constructor :)
        Uri uri = new Uri($"log-example://log/{text}");
        DeepLinkManager.TryRaiseDeepLinkEvent(uri.AbsoluteUri);
    }

    public Result HandleDeepLinkData(DeepLinkData deepLinkData)
    {
        if (deepLinkData is LogTextDeepLinkData logTextDeepLinkData)
        {
            Debug.LogError(logTextDeepLinkData.Text);
            return Result.Handled;
        }

        return Result.Ignore;
    }
}
