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
        private const string PackagePath = "Packages/MPath";
        private const string BuildsDirectory = "builds";
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
            // Ensure builds directory exists
            if (!Directory.Exists(BuildsDirectory))
            {
                Directory.CreateDirectory(BuildsDirectory);
            }
            
            // Get version from package.json
            string packageJsonPath = Path.Combine(PackagePath, "package.json");
            string json = File.ReadAllText(packageJsonPath);
            
            // Parse version using JsonUtility
            PackageInfo packageInfo = JsonUtility.FromJson<PackageInfo>(json);
            string version = packageInfo.version;
            
            // Create the output path
            string outputPath = Path.Combine(BuildsDirectory, string.Format(PackageNameFormat, version));
            
            // Export the package
            AssetDatabase.ExportPackage(
                PackagePath,
                outputPath,
                ExportPackageOptions.Recurse
            );
            
            Debug.Log($"Package exported to: {outputPath}");
        }
    }
} 