using System;
using TimboJimbo.InboundLinkManager;
using TimboJimbo.InboundLinkManager.Handlers;
using UnityEngine;

// Register a deep link scheme
[assembly: InboundLinkCustomScheme("log-example")]

// This is how you would register a domain
[assembly: InboundLinkAssociatedDomain("www.example.com")] 

[InboundLinkParser("/log/")]
public class LogTextInboundLinkData : InboundLinkData
{
    public readonly string Text;
    
    public LogTextInboundLinkData(string deepLink)
    {
        //ie 'log-example://log/Hello World' (The deep link string has been URL Decoded for you already)
        Text = deepLink.Substring(deepLink.LastIndexOf('/') + 1);
    }
}

public class Testing : MonoBehaviour, IInboundLinkHandler
{
    public void Start()
    {
        InboundLinkManager.AddHandler(this, true);
    }

    // Called via UI Button onClick
    public void InjectLogDeepLink(string text)
    {
        //it is not necessary to create a Uri object as TryRaiseDeepLinkEvent accepts a string
        //but this is just to emulate a real world scenario...!
        //The URL (And by extension the deep link data will be URL Encoded...:
        // 'log-example://log/Hello World' -> 'log-example://log/Hello%20World' 
        //But DeepLinkManager will decode it for you before passing it to your DeepLinkData constructor :)
        Uri uri = new Uri($"log-example://log/{text}");
        InboundLinkManager.TryHandleInboundLink(uri.AbsoluteUri);
    }

    public Result HandleInboundLink(InboundLinkData inboundLinkData)
    {
        if (inboundLinkData is LogTextInboundLinkData logTextDeepLinkData)
        {
            Debug.LogError(logTextDeepLinkData.Text);
            return Result.Handled;
        }

        return Result.Ignore;
    }
}
