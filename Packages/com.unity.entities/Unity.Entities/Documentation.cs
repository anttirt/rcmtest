using System;
using System.Diagnostics;
using UnityEngine;
#if UNITY_EDITOR
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
#endif

namespace Unity.Entities
{
    /// <summary>
    /// Attribute to define the help URL for Entities package classes.
    /// </summary>
    /// <example>
    /// [EntitiesHelpURL("conversion-subscenes")]
    /// public class SubScene : MonoBehaviour
    /// </example>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class EntitiesHelpURLAttribute : HelpURLAttribute
    {
        /// <summary>
        /// Creates a help URL attribute for the Entities package documentation.
        /// </summary>
        /// <param name="pageName">The documentation page name (without .html extension).</param>
        /// <param name="pageHash">Optional section anchor on the page.</param>
        public EntitiesHelpURLAttribute(string pageName, string pageHash = "")
            : base(EntitiesDocumentation.GetPageLink(pageName, pageHash))
        {
        }
    }

    /// <summary>
    /// Provides documentation URL generation for the Entities package.
    /// </summary>
    internal static class EntitiesDocumentation
    {
        const string k_PackageName = "com.unity.entities";
        const string k_VersionedUrlFormat = "https://docs.unity3d.com/Packages/{0}@{1}/manual/{2}.html{3}";
        const string k_LatestUrlFormat = "https://docs.unity3d.com/Packages/{0}@latest/index.html?subfolder=/manual/{1}.html{2}";
        static string k_Version = string.Empty;

        /// <summary>
        /// Generates a documentation URL for the given page.
        /// </summary>
        /// <param name="pageName">The page name without extension.</param>
        /// <param name="pageHash">Optional section hash/anchor.</param>
        /// <returns>The full documentation URL.</returns>
        public static string GetPageLink(string pageName, string pageHash = "")
        {
            if (!string.IsNullOrEmpty(pageHash) && !pageHash.StartsWith("#"))
                pageHash = $"#{pageHash}";

#if UNITY_EDITOR
            if (k_Version == string.Empty)
            {
                var packageInfo = PackageInfo.FindForAssembly(typeof(EntitiesDocumentation).Assembly);
                if (packageInfo != null)
                {
                    // Extract Major.Minor from version (e.g., "6.5" from "6.5.0")
                    var version = packageInfo.version;
                    var lastDot = version.LastIndexOf('.');
                    if (lastDot > 0)
                        k_Version = version.Substring(0, lastDot);
                }
            }
            
            if (k_Version != string.Empty)
                return string.Format(k_VersionedUrlFormat, k_PackageName, k_Version, pageName, pageHash);
#endif
            // Fallback to @latest URL format
            return string.Format(k_LatestUrlFormat, k_PackageName, pageName, pageHash);
        }
    }
}