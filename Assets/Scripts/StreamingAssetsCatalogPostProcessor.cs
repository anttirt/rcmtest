#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using System.Linq;
using Unity.Entities.Content;
using System;
using System.Collections.Generic;

#if UNITY_ANDROID
using UnityEditor.Android;
#endif

class StreamingAssetsCatalogPostProcessor
#if UNITY_ANDROID
	: IPostGenerateGradleAndroidProject
#endif
{
	[MenuItem("Assets/Publish/Existing Build (skip StreamingAssets catalog)")]
	static void ExistingBuildMenuItem()
	{
		var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish", Path.GetDirectoryName(Application.dataPath), "Builds");
		if(!string.IsNullOrEmpty(buildFolder))
		{
			var streamingAssetsPath = $"{buildFolder}/{PlayerSettings.productName}_Data/StreamingAssets";
			PublishContent(streamingAssetsPath, $"{buildFolder}-RemoteContent", f => new string[] { "all" });
		}
	}

	static bool PublishContent(string sourceFolder, string targetFolder, Func<string, IEnumerable<string>> contentSetFunc, bool deleteSrcContent = false)
	{
		if (!Directory.Exists(sourceFolder))
		{
			Debug.Log($"PublishContent - Source folder {sourceFolder} does not exist.");
			return false;
		}

		var files = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);
		files = files.Where(f => !f.EndsWith(StreamingAssetsCatalogData.kFilename)).ToArray();
		if (files.Length == 0)
		{
			Debug.Log($"PublishContent - Source folder {sourceFolder} is empty.");
			return false;
		}
		Array.Sort(files);
		return RemoteContentCatalogBuildUtility.PublishContent(files, sourceFolder, targetFolder, contentSetFunc, deleteSrcContent);
	}

	[PostProcessBuild(1)]
	public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuildProject)
	{
		if(buildTarget == BuildTarget.Android)
			return;

		var directory = pathToBuildProject;

		if((File.GetAttributes(directory) & FileAttributes.Directory) == 0)
			directory = Path.GetDirectoryName(directory);

		string streamingAssetsPath;

		switch(buildTarget)
		{
			case BuildTarget.iOS:
				streamingAssetsPath = Path.Combine(directory, "Data", "Raw");
				break;

			case BuildTarget.Android:
				// NOTE: handled by IPostGenerateGradleAndroidProject
				return;

			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64:
			case BuildTarget.StandaloneOSX:
				streamingAssetsPath = Path.Combine(directory, $"{PlayerSettings.productName}_Data", "StreamingAssets");
				break;

			default:
				throw new System.NotImplementedException($"TODO: inject installed catalog for {buildTarget}");
		}

		BuildCatalog(buildTarget, streamingAssetsPath);
	}

#if UNITY_ANDROID
	public int callbackOrder => 0;

	public void OnPostGenerateGradleAndroidProject(string path)
	{
		BuildCatalog(BuildTarget.Android, Path.Combine(path, "src", "main", "assets"));
	}
#endif

	private static void BuildCatalog(BuildTarget buildTarget, string streamingAssetsPath)
	{
		var blobBuilder = new BlobBuilder(Allocator.Temp);
		ref var catalog = ref blobBuilder.ConstructRoot<StreamingAssetsCatalogData>();
		var paths = Directory.EnumerateFiles(streamingAssetsPath, "*.*", SearchOption.AllDirectories).ToArray();
		var array = blobBuilder.Allocate(ref catalog.Entries, paths.Length);
		int index = 0;

		foreach(var path in paths)
		{
			var hash = UnityEngine.Hash128.Compute(File.ReadAllBytes(path));
			var relativePath = Path.GetRelativePath(streamingAssetsPath, path);
			blobBuilder.AllocateString(ref array[index].FilePath, relativePath);
			array[index].ContentHash = hash;
			Debug.Log($"StreamingAssetsCatalog: {relativePath} -> {array[index].ContentHash.ToString()}");
			++index;
		}

		BlobAssetReference<StreamingAssetsCatalogData>.Write(blobBuilder, Path.Combine(streamingAssetsPath, StreamingAssetsCatalogData.kFilename), StreamingAssetsCatalogData.kVersion);
	}
}
#endif
