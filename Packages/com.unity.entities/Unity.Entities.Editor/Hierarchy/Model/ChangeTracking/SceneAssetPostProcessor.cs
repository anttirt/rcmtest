using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Editor.Bridge;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Editor
{
    class SceneAssetPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            using var _ = ListPool<GameObjectChangeTrackerEvent>.Get(out var pooledList);
            foreach (var asset in movedAssets)
            {
                if (Path.GetExtension(asset) == ".unity")
                {
                    var scene = SceneManager.GetSceneByPath(asset);
                    if (scene is { isSubScene: true, isLoaded: false })
                    {
                        Hash128 sceneGuid = AssetDatabase.GUIDFromAssetPath(asset);
                        foreach (var subScene in SubScene.AllSubScenes)
                        {
                            if (subScene.IsLoaded || subScene.SceneGUID != sceneGuid)
                                continue;
                            pooledList.Add(new GameObjectChangeTrackerEvent(subScene.gameObject.GetEntityId(), GameObjectChangeTrackerEventType.UnloadedSubSceneWasRenamed));
                            break;
                        }
                    }
                    else
                    {
                        pooledList.Add(new GameObjectChangeTrackerEvent(EntityId.FromULong(scene.handle.GetRawData()), GameObjectChangeTrackerEventType.SceneWasRenamed));
                    }
                }
            }

            using var events = pooledList.ToNativeArray(AllocatorManager.Temp);
            GameObjectChangeTrackerBridge.PublishEvents(events);
        }
    }
}
