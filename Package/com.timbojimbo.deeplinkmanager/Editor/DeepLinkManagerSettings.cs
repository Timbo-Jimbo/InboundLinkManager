using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TimboJimbo.DeepLinkManager.Editor
{
    [FilePath("ProjectSettings/TJ.DeepLinkManagerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class DeepLinkManagerSettings : ScriptableSingleton<DeepLinkManagerSettings>
    {
        [SerializeField] internal  List<CustomSchemaMetadata> CustomSchemaMetadatas = new();

        [InitializeOnLoadMethod]
        static void Init()
        {
            if (Application.isPlaying) return;

            //wait for other InitializeOnLoadMethods to run (like the DeepLinkManager one.. :P )
            EditorApplication.delayCall += () =>
            {
                //sync update loop
                List<string> iosUrlSchemaList = null;
                
                EditorApplication.update += () =>
                {
                    //no change? no-op
                    if (iosUrlSchemaList != null && iosUrlSchemaList.SequenceEqual(PlayerSettings.iOS.iOSUrlSchemes)) return;
                    
                    ValidatePlayerSettings();
                    iosUrlSchemaList = PlayerSettings.iOS.iOSUrlSchemes.ToList();
                };
            };
            
            void ValidatePlayerSettings()
            {
                var playerSettingsSchemaList = PlayerSettings.iOS.iOSUrlSchemes.ToList();
                var saveRequired = false;

                foreach (var dlmSchema in DeepLinkManager.CustomSchemas)
                {
                    var metadata = instance.CustomSchemaMetadatas.FirstOrDefault(x => x.Schema == dlmSchema);
                    
                    if (metadata == null)
                    {
                        metadata = new CustomSchemaMetadata(dlmSchema);
                        instance.CustomSchemaMetadatas.Add(metadata);
                        saveRequired = true;
                    }
                    
                    var needsToBeAddedToPlayerSettings = !playerSettingsSchemaList.Contains(dlmSchema);
                    if (needsToBeAddedToPlayerSettings && metadata.ManagementMode != SchemaManagementMode.DoNotManage)
                    {
                        var wantsToAdd = EditorUtility.DisplayDialog(
                            title: "Deep Link Manager",
                            message: $"The custom schema '{dlmSchema}' is not defined in the iOS Player Settings. Would you like to add it to the schema list in the iOS Player Settings?",
                            "Yes", "No"
                        );
                        
                        if (wantsToAdd)
                        {
                            metadata.ManagementMode = SchemaManagementMode.Manage;
                            playerSettingsSchemaList.Add(dlmSchema);
                        }
                        else
                        {
                            metadata.ManagementMode = SchemaManagementMode.DoNotManage;
                        }
                        
                        saveRequired = true;
                    }
                }

                for (var i = playerSettingsSchemaList.Count - 1; i >= 0; i--)
                {
                    var psSchema = playerSettingsSchemaList[i];
                    var metadata = instance.CustomSchemaMetadatas.FirstOrDefault(x => x.Schema == psSchema);

                    if (metadata == null)
                    {
                        //This must be a schema the user added themselves, so we dont want to touch it...
                        continue;
                    }

                    // We recognise this schema, but the user is managing it themselves...
                    if (metadata.ManagementMode == SchemaManagementMode.DoNotManage)
                        continue;

                    var requiresRemovalFromPlayerSettings = !DeepLinkManager.CustomSchemas.Contains(psSchema);

                    if (requiresRemovalFromPlayerSettings)
                    {
                        var wantsToRemove = EditorUtility.DisplayDialog(
                            title: "Deep Link Manager",
                            message:
                            $"The custom schema '{psSchema}' is no longer recognised by the DeepLinkManager. Would you like to remove it from the schema list in the iOS Player Settings?",
                            "Yes", "No"
                        );

                        if (wantsToRemove)
                        {
                            metadata.ManagementMode = SchemaManagementMode.Manage;
                            playerSettingsSchemaList.RemoveAt(i);
                        }
                        else
                        {
                            metadata.ManagementMode = SchemaManagementMode.DoNotManage;
                        }
                        
                        saveRequired = true;
                    }
                }

                //dont set every time, as this will trigger a reimport/dirty asset (source control noise)
                if (!PlayerSettings.iOS.iOSUrlSchemes.SequenceEqual(playerSettingsSchemaList))
                    PlayerSettings.iOS.iOSUrlSchemes = playerSettingsSchemaList.ToArray();
                
                //lets get rid of any metadata that is no longer needed
                var metadataToRemove = instance.CustomSchemaMetadatas
                    .Where(x => !DeepLinkManager.CustomSchemas.Contains(x.Schema))
                    .ToList();
                
                if (metadataToRemove.Any())
                {
                    metadataToRemove.ForEach(x => instance.CustomSchemaMetadatas.Remove(x));
                    saveRequired = true;
                }

                if (saveRequired)
                    instance.Save(true);
            };
        }

        internal enum SchemaManagementMode
        {
            Uninitialized,
            Manage,
            DoNotManage
        }
        
        [Serializable]
        internal class CustomSchemaMetadata
        {
            public string Schema;
            public SchemaManagementMode ManagementMode;
            
            public CustomSchemaMetadata(string schema)
            {
                Schema = schema;
                ManagementMode = SchemaManagementMode.Uninitialized;
            }
        }
    }
}