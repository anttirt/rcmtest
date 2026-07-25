using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Scenes;
using Unity.Scenes.Editor;
using Unity.Scenes.Editor.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SubSceneEditorTests
{
    string m_TempAssetDir;

    [OneTimeSetUp]
    public void SetUp()
    {
        var guid = AssetDatabase.CreateFolder("Assets", nameof(SubSceneEditorTests));
        m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
    }

    [TearDown]
    public void TearDown()
    {
        // Triggers OnDisable on SubScenes, removing them from the static AllSubScenes list
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        AssetDatabase.DeleteAsset(m_TempAssetDir);
    }

    static bool IsInAllSubScenes(SubScene subScene)
    {
        foreach (var s in SubScene.AllSubScenes)
        {
            if (s == subScene)
                return true;
        }
        return false;
    }

    SubScene CreateSubScene(string subSceneName, string parentSceneName, InteractionMode interactionMode = InteractionMode.AutomatedAction, SubSceneContextMenu.NewSubSceneMode mode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene)
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(mainScene);

        var path = Path.Combine(m_TempAssetDir, $"{parentSceneName}.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var go = new GameObject();
        go.name = subSceneName;
        Selection.activeGameObject = go;

        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = go,
            newSubSceneMode = mode
        };
        return SubSceneContextMenu.CreateNewSubScene(go.name, args, interactionMode);
    }

    [Test]
    public void CreateEmptySubScene()
    {
        Assert.DoesNotThrow(() => CreateSubScene("EmptySubScene", "ParentScene", InteractionMode.AutomatedAction, SubSceneContextMenu.NewSubSceneMode.EmptyScene));
    }

    [Test]
    public void MissingSubSceneFolder()
    {
        Assert.DoesNotThrow(() => CreateSubScene("SubScene", "whatever"));
    }

    [Test]
    public void ExistingSubSceneFolder()
    {
        Directory.CreateDirectory(Path.Combine(m_TempAssetDir, "MatchingCapitalization"));
        Assert.DoesNotThrow(() => CreateSubScene("SubScene", "MatchingCapitalization"));
    }

    [Test]
    public void WrongCapitalizationSubSceneFolder()
    {
        Directory.CreateDirectory(Path.Combine(m_TempAssetDir, "LOWERCASE"));
        Assert.DoesNotThrow(() =>  CreateSubScene("SubScene", "lowercase"));
    }

    [Test]
    public void InvalidFileNameCharInGameObjectNameThrows()
    {
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("SubScene/Something:", "ParentScene"); }
            , "Invalid file characters should be handled gracefully");
    }

    [Test]
    public void EmptySubSceneNameThrows()
    {
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("", "ParentScene"); }
            , "Empty SubScene name is handled gracefully");
    }

    [Test]
    public void MissingSceneForSubScene_GetSceneName_ReturnsEmptyString()
    {
        var go = new GameObject();
        var subscene = go.AddComponent<SubScene>();
        Assert.IsNull(subscene.SceneAsset);
        Assert.AreEqual(string.Empty, subscene.SceneName);
    }

    [Test]
    public void LeadingAndTrailingWhiteSpacesAreTrimmedFromSubSceneName()
    {
        string subSceneName = " SubScene ";
        SubScene subSceneComponent = CreateSubScene(subSceneName, "ParentScene");
        Assert.IsTrue(subSceneComponent.EditingScene.IsValid(), "Leading and trailing white spaces should be trimmed before creating the Scene asset file");
        Assert.AreEqual(subSceneComponent.EditingScene.name, subSceneName.Trim(), "Leading and trailing white spaces should be trimmed before creating the Scene asset file");
    }

    [Test]
    public void OverwritingExistingSceneFilesArePrevented()
    {
        Assert.IsTrue(CreateSubScene("SubScene", "SameParentScene").EditingScene.IsValid(), "First SubScene should be created");
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("SubScene", "SameParentScene"); }
            , "Trying to create a SubScene with same path as an exising SubScene should be prevented");
    }

    [Test]
    public void RemovingSceneAssetReferenceUnloadsScene()
    {
        var subsceneComponent = CreateSubScene("SubSceneToUnload", "ParentScene");
        Assert.IsTrue(subsceneComponent.EditingScene.isLoaded);

        subsceneComponent.SceneAsset = null;
        Assert.IsFalse(subsceneComponent.EditingScene.isLoaded, "The loaded sub scene should have been unloaded since it is no longer shown in the Hierarchy");
    }

    [Test]
    public void CreateSubSceneFromSelectionKeepsSiblingIndexInHierarchy()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(mainScene);

        var path = Path.Combine(m_TempAssetDir, "ParentScene.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var go1 = new GameObject("go1");
        var go2 = new GameObject("go2");
        var go3 = new GameObject("go3");

        var siblingIndex = go2.transform.GetSiblingIndex();

        Selection.activeGameObject = go2;

        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = Selection.activeGameObject,
            newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene
        };
        var subsceneComponent = SubSceneContextMenu.CreateNewSubScene(args.target.name, args, InteractionMode.AutomatedAction);

        Assert.AreEqual(siblingIndex, subsceneComponent.transform.GetSiblingIndex(), "The resulting SubScene GameObject should have the sibling order in the Hierarchy as the input GameObject.");
    }

    [Test]
    public void CreatingSubSceneFromPartialPrefabInstanceIsNotAllowed()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(mainScene);

        var path = Path.Combine(m_TempAssetDir, "ParentScene.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var go1 = new GameObject("go1");
        var go2 = new GameObject("go2");
        var go3 = new GameObject("go3");
        go2.transform.parent = go1.transform;
        go3.transform.parent = go2.transform;
        PrefabUtility.SaveAsPrefabAssetAndConnect(go1, m_TempAssetDir + "/TestPrefab.prefab", InteractionMode.AutomatedAction);

        Selection.activeGameObject = go2;
        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = Selection.activeGameObject,
            newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene
        };

        Assert.Throws<ArgumentException>(
            () => { SubSceneContextMenu.CreateNewSubScene(args.target.name, args, InteractionMode.AutomatedAction); }
            , "Creating a SubScene from a partial Prefab selection should fail");
    }

    [Test]
    public void CreateSubSceneSupportsUndo()
    {
        var subSceneComponent = CreateSubScene("SubSceneToUndo", "ParentScene", InteractionMode.UserAction);
        Assert.IsTrue(subSceneComponent.EditingScene.isLoaded);

        var rootTransform = subSceneComponent.EditingScene.GetRootGameObjects()[0];
        Assert.IsNotNull(rootTransform, "SubScene should have a root GameObject");
        Assert.IsTrue(rootTransform.gameObject.scene.isSubScene, "The GameObject should now live in a SubScene");
        Assert.AreEqual(subSceneComponent.EditingScene, rootTransform.gameObject.scene);

        Undo.PerformUndo();
        Assert.IsTrue(subSceneComponent == null, "The SubScene component should have been destroyed as part of Undo");
        Assert.IsNotNull(rootTransform, "The root should still be valid after Undo");
        Assert.IsFalse(rootTransform.gameObject.scene.isSubScene, "The GameObject moved to the SubScene should now be back in the parent scene");
    }

    [Test]
    public void SubSceneAssetSavedToNewAssetPath_WillFixUpItsSceneAssetReference()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = Path.Combine(m_TempAssetDir, "MainScene_SavedAs.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var subSceneComponent = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("SubScene1", true, mainScene, () =>
        {
            var go = new GameObject("GameObject1");
            return new List<GameObject> { go };
        });

        var subScenePath = subSceneComponent.EditableScenePath;
        var dir = Path.GetDirectoryName(subScenePath);
        var ext = Path.GetExtension(subScenePath);
        var newPath = Path.Combine(dir, "SubScene2" + ext);
        newPath = newPath.Replace("\\", "/");

        Assert.IsFalse(subSceneComponent.gameObject.scene.isDirty);

        // Save scene to a new path (Simulating File -> Save As menu item for an SubScene set as the Active Scene)
        var subScene = subSceneComponent.EditingScene;
        EditorSceneManager.SaveScene(subScene, newPath, /*saveAsCopy =*/ false);

        Assert.IsTrue(subSceneComponent.gameObject.scene.isDirty);
        var canBeFoundScene = SceneManager.GetSceneByPath(newPath);
        Assert.IsTrue(canBeFoundScene.IsValid());
        Assert.IsTrue(!string.IsNullOrEmpty(subSceneComponent.EditingScene.path), "The SubScene lost its editing scene after it was saved to a new path. This will break authoring.");
        Assert.AreEqual(canBeFoundScene.path, subSceneComponent.EditingScene.path);
        Assert.IsNotNull(subSceneComponent.EditingScene.GetRootGameObjects()[0]);
    }

    [Test]
    public void SubScene_CircularDependency_IsPreventedInOnValidate()
    {
        // Create a main scene
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = Path.Combine(m_TempAssetDir, "CircularDependencyTest.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        // Create a SubScene
        var subSceneGO = new GameObject("SubScene");
        var subSceneComponent = subSceneGO.AddComponent<SubScene>();

        // Try to set the SceneAsset to the same scene the SubScene GameObject is in (circular dependency)
        var mainSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        subSceneComponent.SceneAsset = mainSceneAsset;

        // The GUID should be reset to default to prevent circular dependency
        Assert.That(subSceneComponent.SceneGUID, Is.EqualTo(default(Unity.Entities.Hash128)),
            "SceneGUID should be default when circular dependency is detected");
    }

    [Test, Description("UUM-144079: Clearing SceneAsset left stale SubSceneManager registration until OnDisable")]
    public void ClearingSceneAsset_OnEnabledSubScene_UnregistersFromSubSceneManager()
    {
        var subScene = CreateSubScene("SubSceneToUnregister", "ParentSceneForUnregister");

        Assert.That(SubSceneManager.IsSubScene(subScene.gameObject), Is.True,
            "SubScene should be registered in SubSceneManager after creation");

        subScene.SceneAsset = null;

        Assert.That(SubSceneManager.IsSubScene(subScene.gameObject), Is.False,
            "SubScene should be unregistered from SubSceneManager immediately when SceneAsset is cleared");
    }

    [Test, Description("UUM-144079: Circular dependency left stale SubSceneManager registration")]
    public void SubScene_UnregistersFromSubSceneManager_AfterCircularDependency()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = Path.Combine(m_TempAssetDir, "CircularDependencyUnregisterTest.unity");
        EditorSceneManager.SaveScene(mainScene, path);

  		var subSceneGO = new GameObject("SubScene");
  		var subSceneComponent = subSceneGO.AddComponent<SubScene>();

  		var otherPath = Path.Combine(m_TempAssetDir, "OtherCircularDependencyUnregisterTest.unity");
  		var otherScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
  		EditorSceneManager.SaveScene(otherScene, otherPath);
  		var otherSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(otherPath);
  		subSceneComponent.SceneAsset = otherSceneAsset;
  		Assert.That(SubSceneManager.IsSubScene(subSceneGO), Is.True, "Should be registered before circular assignment");

  		var mainSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
  		subSceneComponent.SceneAsset = mainSceneAsset;

  		Assert.That(subSceneComponent.SceneGUID, Is.EqualTo(default(Unity.Entities.Hash128)), "SceneGUID should be default when circular dependency is detected");
  		Assert.That(SubSceneManager.IsSubScene(subSceneGO), Is.False, "SubScene should be unregistered when circular dependency is detected");
    }

    [Test, Description("UUM-142972: Duplicate SubScenes referencing the same SceneAsset caused a crash")]
    public void DuplicateSubScene_IsNotRegistered_AndLogsWarning()
    {
        var subScene1 = CreateSubScene("SubScene1", "ParentSceneForDuplicate");
        var sceneAsset = subScene1.SceneAsset;

        Assert.That(SubSceneManager.IsSubScene(subScene1.gameObject), Is.True,
            "First SubScene should be registered");
        Assert.IsTrue(IsInAllSubScenes(subScene1),
            "First SubScene should be in AllSubScenes");

        var duplicateGO = new GameObject("DuplicateSubScene");
        LogAssert.Expect(LogType.Warning, new Regex("can not reference the same scene.*multiple times"));
        var duplicateSubScene = duplicateGO.AddComponent<SubScene>();
        duplicateSubScene.SceneAsset = sceneAsset;

        Assert.That(SubSceneManager.IsSubScene(duplicateGO), Is.False,
            "Duplicate SubScene should not be registered with SubSceneManager");
        Assert.IsFalse(IsInAllSubScenes(duplicateSubScene),
            "Duplicate SubScene should not be in AllSubScenes");
        Assert.IsTrue(IsInAllSubScenes(subScene1),
            "Original SubScene should still be in AllSubScenes");
    }

    [Test, Description("UUM-143812: Undoing changes to SubScene GameObject triggered unregister/register cycle that left stale hierarchy mappings")]
    public void UndoSubSceneChange_WithChildren_DoesNotCorruptHierarchyState()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = Path.Combine(m_TempAssetDir, "UndoSubSceneTest.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        // Create a SubScene with child GameObjects
        var subScene = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("SubSceneWithChildren", true, mainScene, () =>
        {
            var parent = new GameObject("Parent");
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");
            child1.transform.SetParent(parent.transform);
            child2.transform.SetParent(parent.transform);
            return new List<GameObject> { parent };
        });

        Assert.That(SubSceneManager.IsSubScene(subScene.gameObject), Is.True,
            "SubScene should be registered after creation");
        Assert.That(subScene.EditingScene.isLoaded, Is.True,
            "SubScene should be open for editing");

        // Make a change to the SubScene GameObject that will trigger OnValidate on undo
        Undo.RecordObject(subScene.gameObject, "Modify SubScene");
        subScene.gameObject.name = "SubSceneWithChildren_Modified";
        Undo.FlushUndoRecordObjects();

        // Perform undo - this triggers OnValidate which unregisters and re-registers the SubScene
        Undo.PerformUndo();

        // Verify SubScene is still properly registered (the fix ensures child mappings are cleaned up)
        Assert.That(SubSceneManager.IsSubScene(subScene.gameObject), Is.True,
            "SubScene should still be registered after undo");
        Assert.That(subScene.gameObject.name, Is.EqualTo("SubSceneWithChildren"),
            "SubScene name should be reverted");

        // Verify we can still access the editing scene and its children
        Assert.That(subScene.EditingScene.isLoaded, Is.True,
            "SubScene editing scene should still be loaded after undo");
        var rootObjects = subScene.EditingScene.GetRootGameObjects();
        Assert.That(rootObjects.Length, Is.EqualTo(1),
            "SubScene should have one root object");
        Assert.That(rootObjects[0].name, Is.EqualTo("Parent"),
            "Root object should be Parent");
        Assert.That(rootObjects[0].transform.childCount, Is.EqualTo(2),
            "Parent should have 2 children");
    }

}
