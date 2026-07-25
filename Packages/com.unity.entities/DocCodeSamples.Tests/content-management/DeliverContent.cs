namespace Doc.CodeSamples.Tests
{
#region example
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

// Loads remote content catalogs, caches them locally, and makes weakly-referenced
// assets available for use by resolving their references to the downloaded content.
public partial struct LoadingRemoteCatalogSystem : ISystem
{
    private bool initialized;
    private FixedString512Bytes remoteUrlRoot;
    private FixedString512Bytes cachePath;
    private FixedString64Bytes contentNameSet;
    private int maxLoadAttempts; // Max number of times to try loading the catalog
    private int loadAttempts;    // Current number of attempts
        
    public void OnCreate(ref SystemState state)
    {
        initialized = false;
        contentNameSet = "all";
        remoteUrlRoot = "https://127.0.0.1/content/";
        cachePath = Application.persistentDataPath + "/content-cache/";
        maxLoadAttempts = 3;
        loadAttempts = 0;
            
        // Register a callback that runs when content delivery completes
        ContentDeliveryGlobalState.RegisterForContentUpdateCompletion(UpdateStateCallback);
    }

    private void UpdateStateCallback(ContentDeliveryGlobalState.ContentUpdateState
        contentUpdateState)
    {
        // Use this condition to implement logic for cases when content update failed and
        // there is no data in the cache, or for an early exit.
        if (contentUpdateState == ContentDeliveryGlobalState.ContentUpdateState
                .NoContentAvailable)
        {
            // The system attempts again until it reaches the maximum number of attempts.
            return;
        }

        // Track the state of the content and define when it's ready to use
        if (contentUpdateState >= ContentDeliveryGlobalState.ContentUpdateState
                .ContentReady)
        {
            // Only mark initialized when catalog loading actually succeeded.
            initialized = true;
            LoadMainScene();
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        // Attempt to load content a content catalog until it is initialized or the maximum
        // number of attempts is reached.
        if (!initialized && loadAttempts < maxLoadAttempts)
        {
            loadAttempts++;
            // Calling the LoadContentCatalog method initializes the content catalog.
            RuntimeContentSystem.LoadContentCatalog(remoteUrlRoot.ToString(),
                cachePath.ToString(), contentNameSet.ToString(), true);
        }
    }

    private void LoadMainScene()
    {
        // Content is ready - load your main scene or enable gameplay systems here
    }
}
#endregion
}