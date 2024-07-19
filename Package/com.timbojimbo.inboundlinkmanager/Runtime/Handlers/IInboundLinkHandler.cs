namespace TimboJimbo.InboundLinkManager.Handlers
{
    public interface IInboundLinkHandler
    {
        Result HandleInboundLink(InboundLinkData inboundLinkData);
    }
}