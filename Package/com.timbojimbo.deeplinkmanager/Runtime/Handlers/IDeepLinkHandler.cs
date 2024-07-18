namespace TimboJimbo.DeepLinkManager.Handlers
{
    public interface IDeepLinkHandler
    {
        Result HandleDeepLinkData(DeepLinkData deepLinkData);
    }
}