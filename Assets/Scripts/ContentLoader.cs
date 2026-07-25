using System.Collections;
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ContentLoader : MonoBehaviour
{
	private static string ContentCachePath => System.IO.Path.Combine(Application.persistentDataPath, "ContentCache");

	const string kAllContent = "all";
	const string k_EntitySceneSubDir = "EntityScenes";

	void Start()
	{
		ContentDeliveryGlobalState.LogFunc = msg => Debug.Log(msg);

		StartCoroutine(LoadContentAndChangeScene());
	}

	IEnumerator LoadContentAndChangeScene()
	{
		RuntimeContentSystem.LoadContentCatalog(null, null, kAllContent);

		if(ContentDeliveryGlobalState.DeliveryService != null)
		{
			Debug.Log("Downloading content");

			while(ContentDeliveryGlobalState.CurrentContentUpdateState < ContentDeliveryGlobalState.ContentUpdateState.DownloadingContentSet)
			{
				yield return null;
			}

			Debug.Log("Loading content");

			while(ContentDeliveryGlobalState.CurrentContentUpdateState < ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
			{
				yield return null;
			}

			Debug.Log("Content loaded");
		}
		else
		{
			Debug.Log("No delivery service");
		}

		SceneManager.LoadScene(1, LoadSceneMode.Single);
	}
}
