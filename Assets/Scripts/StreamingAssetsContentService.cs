using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

class StreamingAssetsContentService : ContentDownloadService
{
	Dictionary<Unity.Entities.Hash128, string> streamingAssetsFiles;

	public StreamingAssetsContentService(string name, BlobAssetReference<StreamingAssetsCatalogData> catalog, int priority = 1000)
		: base(name, cacheDir: "", priority, maxActiveDownloads: int.MaxValue, () => StreamingAssetsDownloadOperation.Instance)
	{
		streamingAssetsFiles = new();

		ref var catalog_ = ref catalog.Value;
		for(int i = 0; i < catalog_.Entries.Length; ++i)
		{
			var hash = catalog_.Entries[i].ContentHash;
			var path = catalog_.Entries[i].FilePath.ToString();
			Debug.Log($"StreamingAssetsContentService: have {path} -> {hash}");
			streamingAssetsFiles.Add(hash, path);
		}
	}

	public override bool CanDownload(RemoteContentLocation loc)
	{
		return streamingAssetsFiles.TryGetValue(loc.Hash, out var path) && File.Exists(Path.Combine(Application.streamingAssetsPath, path));
	}

	public override string ComputeCachePath(RemoteContentLocation loc)
	{
		if(streamingAssetsFiles.TryGetValue(loc.Hash, out var path))
			return Path.Combine(Application.streamingAssetsPath, path);

		return string.Empty;
	}

	[NoAutoStaticsCleanup]
	class StreamingAssetsDownloadOperation : DownloadOperation
	{
		public override bool Process(ref DownloadStatus status, ref long downloadedBytes) { return true; }
		protected override bool ProcessDownload(ref long downloadedBytes, ref string error) { return true; }
		protected override void StartDownload(string remotePath, string localTmpPath) { }
		protected override void CancelDownload() { }

		public static readonly StreamingAssetsDownloadOperation Instance = new();
	}
}

