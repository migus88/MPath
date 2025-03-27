using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MPath.Editor
{
    /// <summary>
    /// Copies the Migs.MPath.Tools.dll to the Unity Plugins folder after a successful build
    /// </summary>
    public class MPathToolsBuilder : IPostprocessBuildWithReport
    {
        // Lower numbers get executed first
        public int callbackOrder { get { return 0; } }

        // Manual copy command for editor use
        [MenuItem("Tools/MPath/Copy Tools DLL to Plugins")]
        public static void CopyToolsDllToPlugins()
        {
            string sourceDll = GetSourceDllPath();
            string destinationDll = GetDestinationDllPath();
            
            if (!File.Exists(sourceDll))
            {
                Debug.LogError($"Could not find source DLL at: {sourceDll}");
                return;
            }
            
            try
            {
                // Ensure Plugins directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationDll));
                
                // Copy the file
                File.Copy(sourceDll, destinationDll, true);
                Debug.Log($"Successfully copied Migs.MPath.Tools.dll to Plugins folder: {destinationDll}");
                
                // Refresh AssetDatabase to see the new file
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to copy Migs.MPath.Tools.dll: {ex.Message}");
            }
        }
        
        // Called after a build has completed
        public void OnPostprocessBuild(BuildReport report)
        {
            // Only copy DLL if build was successful
            if (report.summary.result == BuildResult.Succeeded)
            {
                CopyToolsDllToPlugins();
            }
        }
        
        // Get the path to the source DLL in the Tools project
        private static string GetSourceDllPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string toolsProject = Path.Combine(projectRoot, "src", "mpath-source", "Migs.MPath.Tools");
            
            // Try release first, then debug if not found
            string releaseDll = Path.Combine(toolsProject, "bin", "Release", "net7.0", "Migs.MPath.Tools.dll");
            string debugDll = Path.Combine(toolsProject, "bin", "Debug", "net7.0", "Migs.MPath.Tools.dll");
            
            return File.Exists(releaseDll) ? releaseDll : debugDll;
        }
        
        // Get the destination path in the Plugins folder
        private static string GetDestinationDllPath()
        {
            return Path.Combine(Application.dataPath, "Plugins", "Migs.MPath.Tools.dll");
        }
    }
} 