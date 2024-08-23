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
                
                var entitlementsFileName = pbxProject.GetBuildPropertyForAnyConfig(pbxProject.GetUnityMainTargetGuid(), "CODE_SIGN_ENTITLEMENTS");
                if (string.IsNullOrEmpty(entitlementsFileName)) entitlementsFileName = $"{PlayerSettings.productName.Replace(" ", "_")}.entitlements";
                
                var capManager = new ProjectCapabilityManager(projPath, entitlementsFileName, "Unity-iPhone");

                var appLinks = InboundLinkManager.AssociatedDomains
                    .Select(x => $"applinks:{x}")
                    .ToList();

                if(appLinks.Any())
                {
                    capManager.AddPushNotifications(false);
                    capManager.AddAssociatedDomains(appLinks.ToArray());
                }
                
                capManager.WriteToFile();
            }
        }
    }
}

#endif