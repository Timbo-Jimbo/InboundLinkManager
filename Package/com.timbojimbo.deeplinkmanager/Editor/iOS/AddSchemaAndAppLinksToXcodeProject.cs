#if UNITY_IOS

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TimboJimbo.DeepLinkManager.Editor.iOS
{
    internal class AddSchemaAndAppLinksToXcodeProject
    {
        private static readonly NamedLogger _logger = new ("DeepLinkManager[XCode Project Patcher]");

        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.iOS)
            {
                string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
                PBXProject pbxProject = new PBXProject();
                pbxProject.ReadFromFile(projPath);
                
                string targetGuid = pbxProject.GetUnityMainTargetGuid();

                var appLinks = DeepLinkManager.Hosts
                    .Select(x => $"applinks:{x}")
                    .ToList();

                if(appLinks.Any())
                {
                    pbxProject.AddCapability(targetGuid, PBXCapabilityType.AssociatedDomains);
                
                    string entitlementsPath = pbxProject.GetBuildPropertyForAnyConfig(targetGuid, "CODE_SIGN_ENTITLEMENTS");
                    if (string.IsNullOrEmpty(entitlementsPath))
                    {
                        // Ideally we would not have this hardcoded...! But PBXProject does not expose the target name...?
                        string targetName = "Unity-iPhone"; 
                    
                        //sanity check
                        if(pbxProject.TargetGuidByName(targetName) != targetGuid)
                            throw new System.Exception("Could not find the target name for the main target.");
                    
                        entitlementsPath = $"{targetName}/{targetName}.entitlements";
                        pbxProject.AddFile(entitlementsPath, entitlementsPath);
                        pbxProject.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsPath);
                    }
                
                    // Add Associated Domains to entitlements
                    string entitlementsFullPath = Path.Combine(pathToBuiltProject, entitlementsPath);
                    PlistDocument entitlements = new PlistDocument();
                    entitlements.ReadFromFile(entitlementsFullPath);

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
                }
                
                var schemas = DeepLinkManager.CustomSchemas
                    .Select(x => x + "://")
                    .ToList();

                if (schemas.Any())
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

                    // Add missing schemas
                    var alreadyDefinedSchemas = mainEntrySchemesArray.values.Select(x => x.AsString()).ToList();
                    foreach (var schema in schemas.Except(alreadyDefinedSchemas))
                    {
                        _logger.Log($"Adding URL Scheme: {schema}");
                        mainEntrySchemesArray.AddString(schema);
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