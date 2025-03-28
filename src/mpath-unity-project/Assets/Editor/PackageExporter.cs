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
            try
            {
                Debug.Log("Starting ExportPackageFromBatchMode");
                ExportPackageInternal();
                Debug.Log("ExportPackageFromBatchMode completed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in ExportPackageFromBatchMode: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
                EditorApplication.Exit(1);
                return;
            }
            
            EditorApplication.Exit(0);
        }
        
        private static void ExportPackageInternal()
        {
            string sourcePath = Path.Combine(PackagesPath, MPathFolder);
            string targetPath = Path.Combine(AssetsPath, MPathFolder);
            
            Debug.Log($"Package source path: {sourcePath}, exists: {Directory.Exists(sourcePath)}");
            
            try
            {
                // Get version from package.json before moving folders
                string packageJsonPath = Path.Combine(PackagesPath, MPathFolder, "package.json");
                Debug.Log($"Reading package.json from: {packageJsonPath}, exists: {File.Exists(packageJsonPath)}");
                
                string json = File.ReadAllText(packageJsonPath);
                Debug.Log($"Package.json content: {json}");
                
                // Parse version using JsonUtility
                PackageInfo packageInfo = JsonUtility.FromJson<PackageInfo>(json);
                if (packageInfo == null)
                {
                    Debug.LogError("Failed to parse package.json");
                    return;
                }
                
                string version = packageInfo.version;
                Debug.Log($"Parsed version: {version}");
                
                // Ensure builds directory exists
                var buildsDirectory = Path.Combine(Application.dataPath, "../../../builds");
                
                Debug.Log($"Full builds directory path: {buildsDirectory}");
                
                if (!Directory.Exists(buildsDirectory))
                {
                    Debug.Log($"Creating builds directory: {buildsDirectory}");
                    Directory.CreateDirectory(buildsDirectory);
                }
                
                // Move MPath from Packages to Assets
                Debug.Log("Moving MPath from Packages to Assets...");
                
                // Make sure Assets/MPath doesn't already exist
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }
                
                // List files in source directory
                Debug.Log("Files in source directory:");
                foreach (var file in Directory.GetFiles(sourcePath))
                {
                    Debug.Log($" - {Path.GetFileName(file)}");
                }
                
                // Move the directory
                try
                {
                    Debug.Log($"Moving from {sourcePath} to {targetPath}");
                    Directory.Move(sourcePath, targetPath);
                    AssetDatabase.Refresh();
                    Debug.Log("Move completed successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to move directory: {ex.Message}");
                    Debug.LogError($"Exception details: {ex.GetType().Name}");
                    return;
                }
                
                // Create the output path
                string outputPath = Path.Combine(buildsDirectory, string.Format(PackageNameFormat, version));
                Debug.Log($"Output path: {outputPath}");
                
                try
                {
                    // Export the package - now from Assets/MPath
                    string exportPath = Path.Combine(AssetsPath, MPathFolder);
                    Debug.Log($"Exporting from: {exportPath}");
                    
                    AssetDatabase.ExportPackage(
                        exportPath,
                        outputPath,
                        ExportPackageOptions.Recurse
                    );
                    
                    Debug.Log($"Package exported to: {outputPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to export package: {ex.Message}");
                    Debug.LogError($"Stack trace: {ex.StackTrace}");
                }
                finally
                {
                    // Move MPath back to Packages folder
                    Debug.Log("Moving MPath back to Packages...");
                    try
                    {
                        Debug.Log($"Moving from {targetPath} back to {sourcePath}");
                        Directory.Move(targetPath, sourcePath);
                        AssetDatabase.Refresh();
                        Debug.Log("Move back completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to move directory back: {ex.Message}. Manual intervention required!");
                        Debug.LogError($"Exception details: {ex.GetType().Name}");
                        Debug.LogError($"Stack trace: {ex.StackTrace}");
                    }
                    
                    // Ensure Assets/MPath is deleted in case it still exists
                    try
                    {
                        if (Directory.Exists(targetPath))
                        {
                            Debug.Log($"Target directory still exists after move back. Deleting: {targetPath}");
                            Directory.Delete(targetPath, true); // true for recursive delete
                            AssetDatabase.Refresh();
                            Debug.Log("Successfully deleted target directory");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to delete target directory: {ex.Message}");
                        Debug.LogError($"Exception details: {ex.GetType().Name}");
                        Debug.LogError($"Stack trace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Global error in ExportPackageInternal: {ex.Message}");
                Debug.LogError($"Exception type: {ex.GetType().Name}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
} 