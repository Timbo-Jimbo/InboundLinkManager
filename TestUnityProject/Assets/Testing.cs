using System;
using TimboJimbo.InboundLinkManager;
using TimboJimbo.InboundLinkManager.Handlers;
using UnityEngine;

// Register a scheme
[assembly: InboundLinkCustomScheme("log-example")]

// Associate a domain
[assembly: InboundLinkAssociatedDomain("www.example.com")] 

// Register a parser for an inbound link
[InboundLinkParser("/log/")]
public class LogTextInboundLink : InboundLinkData
{
    public readonly string Text;
    
    public LogTextInboundLink(string inboundLink)
    {
        // With our configuration above, the inbound link can be any one of these:
        // 'log-example://log/Hello World'
        // 'http://www.example.com/log/Hello World'
        // 'https://www.example.com/log/Hello World'
        // (Note: The inbound link string has been URL Decoded for you already, so
        // you don't need to worry about receiving something like 'Hello%20World')
        Text = inboundLink.Substring(inboundLink.LastIndexOf('/') + 1);
    }
}

public class Testing : MonoBehaviour, IInboundLinkHandler
{
    public void Start()
    {
        InboundLinkManager.AddHandler(this, true);
    }

    // Called via UI Button onClick
    public void InjectInboundLink(string text)
    {
        // Converted to URI just to demonstrate URL decoding on the parser side
        Uri uri = new Uri($"log-example://log/{text}");
        InboundLinkManager.TryHandleInboundLink(uri.AbsoluteUri);
    }

    public Result HandleInboundLink(InboundLinkData inboundLinkData)
    {
        if (inboundLinkData is LogTextInboundLink logTextInboundLink)
        {
            Debug.LogError(logTextInboundLink.Text);
            return Result.Handled;
        }

        return Result.Ignore;
    }
}
