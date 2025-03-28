using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace Migs.MPath.Editor
{
    [Serializable]
    public class PackageInfo
    {
        public string version;
    }
    
    public static class PackageExporter
    {
        private const string PackagesPath = "Packages";
        private const string AssetsPath = "Assets";
        private const string MPathFolder = "MPath";
        private const string PackageNameFormat = "migs-mpath-{0}.unitypackage";
        
        [MenuItem("MPath/Export Package")]
        public static void ExportPackage()
        {
            ExportPackageInternal();
        }
        
        // Public method for CI/batch mode execution
        public static void ExportPackageFromBatchMode()
        {
            ExportPackageInternal();
            EditorApplication.Exit(0);
        }
        
        private static void ExportPackageInternal()
        {
            // Get version from package.json before moving folders
            var packageJsonPath = Path.Combine(PackagesPath, MPathFolder, "package.json");
            var json = File.ReadAllText(packageJsonPath);
            
            // Parse version using JsonUtility
            var packageInfo = JsonUtility.FromJson<PackageInfo>(json);
            var version = packageInfo.version;
            
            // Ensure builds directory exists
            var buildsDirectory = Path.Combine(Application.dataPath, "../../../builds");
            
            if (!Directory.Exists(buildsDirectory))
            {
                Directory.CreateDirectory(buildsDirectory);
            }
            
            // Move MPath from Packages to Assets
            Debug.Log("Moving MPath from Packages to Assets...");
            var sourcePath = Path.Combine(PackagesPath, MPathFolder);
            var targetPath = Path.Combine(AssetsPath, MPathFolder);
            
            // Make sure Assets/MPath doesn't already exist
            if (Directory.Exists(targetPath))
            {
                Debug.LogError($"Target directory already exists: {targetPath}");
                return;
            }
            
            // Move the directory
            try
            {
                Directory.Move(sourcePath, targetPath);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to move directory: {ex.Message}");
                return;
            }
            
            // Create the output path
            var outputPath = Path.Combine(buildsDirectory, string.Format(PackageNameFormat, version));
            
            try
            {
                // Export the package - now from Assets/MPath
                AssetDatabase.ExportPackage(
                    Path.Combine(AssetsPath, MPathFolder),
                    outputPath,
                    ExportPackageOptions.Recurse
                );
                
                Debug.Log($"Package exported to: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export package: {ex.Message}");
            }
            finally
            {
                // Move MPath back to Packages folder
                Debug.Log("Moving MPath back to Packages...");
                try
                {
                    Directory.Move(targetPath, sourcePath);
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to move directory back: {ex.Message}. Manual intervention required!");
                }
            }
        }
    }
} 