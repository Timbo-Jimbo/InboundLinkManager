#if UNITY_IOS
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace TimboJimbo.InboundLinkManager.Editor.iOS
{
    internal class AddCustomSchemeAndAssociatedDomainsToXcodeProject
    {
        private static readonly NamedLogger _logger = new ($"{nameof(InboundLinkManager)}[XCode Project Patcher]");

        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.iOS)
            {
                string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
                PBXProject pbxProject = new PBXProject();
                pbxProject.ReadFromFile(projPath);
                
                string targetGuid = pbxProject.GetUnityMainTargetGuid();

                var appLinks = InboundLinkManager.AssociatedDomains
                    .Select(x => $"applinks:{x}")
                    .ToList();

                if(appLinks.Any())
                {
                    pbxProject.AddCapability(targetGuid, PBXCapabilityType.AssociatedDomains);
                
                    // Ideally we would not have this hardcoded...! But PBXProject does not expose the target name...?
                    string targetName = "Unity-iPhone"; 
                    
                    string entitlementsPath = $"{pathToBuiltProject}/{targetName}/{targetName}.entitlements";
                    PlistDocument entitlements = new PlistDocument();
                    if (File.Exists(entitlementsPath))
                        entitlements.ReadFromFile(entitlementsPath);
                
                    // Add Associated Domains to entitlements
                    PlistElementArray associatedDomains = entitlements.root.CreateArray("com.apple.developer.associated-domains");
                    var alreadyDefinedDomains = associatedDomains.values.Select(x => x.AsString()).ToList();

                    foreach (var appLink in appLinks)
                    {
                        if (!alreadyDefinedDomains.Contains(appLink))
                        {
                            _logger.Log($"Adding Associated Domain: {appLink}");
                            associatedDomains.AddString(appLink);
                        }
                    }
                    
                    entitlements.WriteToFile(entitlementsPath);
                    
                    //write the changes back to the entitlements file
                    var entitlementFileName = Path.GetFileName(entitlementsPath);
                    var relativeDestination = targetName + "/" + entitlementFileName;
                    pbxProject.AddFile(relativeDestination, entitlementFileName);
                    pbxProject.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", relativeDestination);
                }

                var schemes = InboundLinkManager.CustomSchemes;

                if (schemes.Any())
                {
                    // Load the Info.plist file
                    string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
                    PlistDocument plist = new PlistDocument();
                    plist.ReadFromFile(plistPath);

                    // Create the CFBundleURLTypes array if it doesn't exist
                    PlistElementArray urlTypes;
                    if (plist.root["CFBundleURLTypes"] == null)
                        urlTypes = plist.root.CreateArray("CFBundleURLTypes");
                    else
                        urlTypes = plist.root["CFBundleURLTypes"].AsArray();

                    var iosBundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
                    var mainEntry = urlTypes.values
                        .FirstOrDefault(v => v.AsDict()?["CFBundleURLName"]?.AsString() == iosBundleIdentifier)
                        ?.AsDict();
                    
                    if (mainEntry == null)
                    {
                        mainEntry = urlTypes.AddDict();
                        mainEntry.SetString("CFBundleURLName", iosBundleIdentifier);
                        mainEntry.SetString("CFBundleTypeRole", "Editor");
                    }
                    
                    var mainEntrySchemesArray = mainEntry["CFBundleURLSchemes"]?.AsArray();
                    if (mainEntrySchemesArray == null)
                        mainEntrySchemesArray = mainEntry.CreateArray("CFBundleURLSchemes");

                    // Add missing schemes
                    var alreadyDefinedSchemes = mainEntrySchemesArray.values.Select(x => x.AsString()).ToList();
                    foreach (var scheme in schemes.Except(alreadyDefinedSchemes))
                    {
                        _logger.Log($"Adding URL Scheme: {scheme}");
                        mainEntrySchemesArray.AddString(scheme);
                    }

                    // Write the changes back to the Info.plist file
                    plist.WriteToFile(plistPath);
                }
                
                pbxProject.WriteToFile(projPath);
            }
        }
    }
}

#endif