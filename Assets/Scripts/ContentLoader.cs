using System.Collections;
using System.IO;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ContentLoader : MonoBehaviour
{
	const string kAllContent = "all";

	void Start()
	{
		StartCoroutine(LoadContentAndChangeScene());
	}

	IEnumerator LoadContentAndChangeScene()
	{
		ContentDeliveryGlobalState.LogFunc = msg => Debug.Log(msg);

		string remoteUrlRoot = null;
		string localCachePath = null;

		if(!Application.isEditor)
		{
			remoteUrlRoot = GetRemoteContentPublishURL();
			localCachePath = Path.Combine(Application.persistentDataPath, "ContentCache");

			Directory.Delete(localCachePath, true);
		}

		RuntimeContentSystem.LoadContentCatalog(
			remoteUrlRoot: remoteUrlRoot,
			localCachePath: localCachePath,
			initialContentSet: kAllContent);

		if(ContentDeliveryGlobalState.DeliveryService != null)
		{
			Debug.Log("Downloading content");

			while(ContentDeliveryGlobalState.CurrentContentUpdateState <= ContentDeliveryGlobalState.ContentUpdateState.DownloadingContentSet)
			{
				yield return null;
			}

			Debug.Log("Loading content");

			while(ContentDeliveryGlobalState.CurrentContentUpdateState < ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
			{
				yield return null;
			}

			int entryCount = 0;
			long totalBytes = 0;
			long cachedBytes = 0;
			long uncachedBytes = 0;
			ContentDeliveryGlobalState.DeliveryService.AccumulateContentSize(kAllContent, ref entryCount, ref totalBytes, ref cachedBytes, ref uncachedBytes);

			Debug.Log($"{ContentDeliveryGlobalState.CurrentContentUpdateState}: entryCount={entryCount} totalBytes={totalBytes} cachedBytes={cachedBytes} uncachedBytes={uncachedBytes}");
		}
		else
		{
			Debug.Log("No delivery service");
		}

		SceneManager.LoadScene(1, LoadSceneMode.Single);
	}

	private static string GetRemoteContentPublishURL()
	{
		// Reconstruct the path used by Assets -> Publish -> Existing Build
		var installPath = Path.GetDirectoryName(Application.dataPath);
		var installName = Path.GetFileName(installPath);
		var remoteContentPath = Path.Combine(Path.GetDirectoryName(installPath), $"{installName}-RemoteContent");
		var fullRemoteContentPath = Path.GetFullPath(remoteContentPath).Replace('\\', '/');
		if(fullRemoteContentPath[0] != '/')
			fullRemoteContentPath = "/" + fullRemoteContentPath;

		return $"file://{fullRemoteContentPath}/";
	}
}
