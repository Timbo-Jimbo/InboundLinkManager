# Inbound Link Manager

A Unity package that handles inbound deep links and universal links with minimal setup. Define your associated domains, custom URL schemes, and path parsers once — the package automatically patches your Xcode project (Universal Links + Deep Links) and Android Manifest (App Links + Deep Links) at build time, then routes incoming links to your code at runtime.

- ✅ **iOS** – patches `Info.plist` URL schemes and adds the Associated Domains entitlement automatically
- ✅ **Android** – injects the correct `<intent-filter>` blocks into `AndroidManifest.xml` automatically
- ✅ **AppsFlyer** – optional, auto-detected (no extra configuration needed)
- ✅ **Editor** – works in Play Mode for easy local testing

---

## Installation

Add the package via the Unity Package Manager using the git URL:

```
https://github.com/Timbo-Jimbo/InboundLinkManager.git?path=Package/com.timbojimbo.inboundlinkmanager
```

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Paste the URL above and click **Add**

---

## Quick Start

### 1 — Declare your domains and schemes

Use assembly-level attributes in any `.cs` file to register the domains and custom URL schemes your app handles. A good place for these is an `AssemblyInfo.cs` file or alongside your parser classes.

```csharp
// Register one or more associated domains (used for Universal Links / App Links)
[assembly: InboundLinkAssociatedDomain("www.example.com")]
[assembly: InboundLinkAssociatedDomain("staging.example.com")]

// Register one or more custom URL schemes (used for Deep Links)
[assembly: InboundLinkCustomScheme("myapp")]
```

### 2 — Define a parser

Create a class that inherits from `InboundLinkData` and decorate it with `[InboundLinkParser("/your/path/")]`. The constructor receives the full inbound URL string (already URL-decoded).

```csharp
using TimboJimbo.InboundLinkManager;

// This parser is triggered whenever an inbound link contains "/invite/"
[InboundLinkParser("/invite/")]
public class InviteLinkData : InboundLinkData
{
    public readonly string InviteCode;

    public InviteLinkData(string inboundLink)
    {
        // Works for all registered domains and schemes, e.g.:
        //   myapp://invite/ABC123
        //   https://www.example.com/invite/ABC123
        InviteCode = inboundLink.Substring(inboundLink.LastIndexOf('/') + 1);
    }
}
```

### 3 — Handle the parsed link

Implement `IInboundLinkHandler` on any class (e.g. a `MonoBehaviour`) and register it with `InboundLinkManager`. Return `Result.Handled` to consume the link or `Result.Ignore` to pass it on to other handlers.

```csharp
using TimboJimbo.InboundLinkManager;
using TimboJimbo.InboundLinkManager.Handlers;
using UnityEngine;

public class InviteScreen : MonoBehaviour, IInboundLinkHandler
{
    private void OnEnable()
    {
        // true = also process any links that arrived before this handler was registered
        InboundLinkManager.AddHandler(this, processQueued: true);
    }

    private void OnDisable()
    {
        InboundLinkManager.RemoveHandler(this);
    }

    public Result HandleInboundLink(InboundLinkData data)
    {
        if (data is InviteLinkData invite)
        {
            Debug.Log($"Invite code received: {invite.InviteCode}");
            // ... show invite UI, join lobby, etc.
            return Result.Handled;
        }

        return Result.Ignore;
    }
}
```

---

## Concepts

### Parsers

A parser is a class that:

- Inherits from `InboundLinkData`
- Is decorated with `[InboundLinkParser("/path/")]`
- Has a constructor that accepts a single `string` (the inbound URL, already URL-decoded)

The path string is matched as a substring of the inbound URL, so `"/invite/"` matches `https://www.example.com/invite/ABC` and `myapp://invite/ABC`.

You can register multiple parsers for different paths:

```csharp
[InboundLinkParser("/invite/")]
public class InviteLinkData : InboundLinkData { ... }

[InboundLinkParser("/promo/")]
public class PromoLinkData : InboundLinkData { ... }

[InboundLinkParser("/share/")]
public class ShareLinkData : InboundLinkData { ... }
```

### Handlers

Handlers receive parsed `InboundLinkData` objects and decide whether to act on them. Multiple handlers can be active at the same time; they are tried in reverse registration order (most recently added first).

**Manual add/remove:**

```csharp
InboundLinkManager.AddHandler(handler, processQueued: true);
InboundLinkManager.RemoveHandler(handler);
```

**Scoped (auto-removes on `Dispose`):**

```csharp
using var _ = InboundLinkManager.AddHandlerScope(handler, processQueued: true);
```

### Unhandled link queue

If a link arrives before any handler is registered (e.g. on a cold start), it is added to `InboundLinkManager.UnhandledInboundLinks`. Passing `processQueued: true` to `AddHandler` / `AddHandlerScope` automatically drains this queue.

```csharp
// Inspect queued links manually if needed
foreach (var link in InboundLinkManager.UnhandledInboundLinks)
    Debug.Log(link);
```

### Manual link injection

Useful for testing or for custom link sources:

```csharp
bool handled = InboundLinkManager.TryHandleInboundLink("myapp://invite/TEST99");
bool canHandle = InboundLinkManager.CanHandleInboundLink("myapp://invite/TEST99");
```

---

## Platform Setup

### iOS

No manual steps required. At build time the package automatically:

- Adds each registered custom scheme to `CFBundleURLSchemes` in `Info.plist`
- Adds the `Associated Domains` capability with `applinks:<domain>` entries for each registered domain

Make sure your **Apple Developer** account has Associated Domains enabled for your App ID, and that you have an `apple-app-site-association` file hosted at `https://<domain>/.well-known/apple-app-site-association`.

### Android

No manual steps required. At build time the package automatically adds `<intent-filter>` blocks to `AndroidManifest.xml`:

- **App Links** (`http`/`https`) – one filter per registered domain × path prefix combination
- **Deep Links** (custom schemes) – one filter covering all custom scheme URLs

For App Links to work you must host a `/.well-known/assetlinks.json` file on each registered domain.

---

## AppsFlyer Integration

If the `AppsFlyer Unity Plugin` is present in your project (detected via the `APPSFLYER_UNITY` scripting define symbol), the package automatically switches to an AppsFlyer-backed link source on device builds. No additional configuration is needed.

To override the source manually:

```csharp
// Switch to a custom source
InboundLinkManager.SetInboundLinkSource(myCustomSource);

// Revert to the default (AppsFlyer if available, otherwise Unity's Application.deepLinkActivated)
InboundLinkManager.ResetInboundLinkSource();
```

Implement `IInboundLinkSource` to provide your own link source:

```csharp
using System;
using TimboJimbo.InboundLinkManager.Sources;

public class MyCustomLinkSource : IInboundLinkSource
{
    public event Action<string> OnInboundLinkReceived;

    public void Activate()
    {
        // Start listening for links and invoke OnInboundLinkReceived(url)
    }

    public void Deactivate()
    {
        // Stop listening
    }
}
```

---

## Full Example

```csharp
using TimboJimbo.InboundLinkManager;
using TimboJimbo.InboundLinkManager.Handlers;
using UnityEngine;

// -- Assembly-level declarations (any .cs file in your assembly) --
[assembly: InboundLinkAssociatedDomain("www.example.com")]
[assembly: InboundLinkCustomScheme("myapp")]

// -- Parser --
[InboundLinkParser("/invite/")]
public class InviteLinkData : InboundLinkData
{
    public readonly string Code;

    public InviteLinkData(string url)
    {
        Code = url.Substring(url.LastIndexOf('/') + 1);
    }
}

// -- Handler --
public class GameManager : MonoBehaviour, IInboundLinkHandler
{
    private void OnEnable() => InboundLinkManager.AddHandler(this, processQueued: true);
    private void OnDisable() => InboundLinkManager.RemoveHandler(this);

    public Result HandleInboundLink(InboundLinkData data)
    {
        if (data is InviteLinkData invite)
        {
            Debug.Log($"Joining with invite code: {invite.Code}");
            return Result.Handled;
        }
        return Result.Ignore;
    }
}
```

When a user opens `https://www.example.com/invite/ABC123` or `myapp://invite/ABC123`, Unity routes it through the parser and delivers an `InviteLinkData` object to any active handler.

---

## License

[MIT](LICENSE)
