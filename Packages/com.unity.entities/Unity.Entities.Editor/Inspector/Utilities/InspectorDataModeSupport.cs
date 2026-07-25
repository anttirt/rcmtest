using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Editor.Bridge;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    static class InspectorDataModeSupport
    {
        static readonly DataMode[] k_EditorDataModes =  { DataMode.Runtime };
        static readonly DataMode[] k_RuntimeDataModes = { DataMode.Runtime };

        static readonly ProfilerMarker k_GetEditorMarker = new("GetEditor");
        static readonly ProfilerMarker k_SelectionCompareMarker = new("Compare Selection");
        static readonly ProfilerMarker k_SelectEditorMarker = new("Select Editor");
        static readonly string k_MixedDataModeWarningMessage = L10n.Tr("Enter Play mode to see live values in Mixed DataMode.");

        static int s_LastSelectionCount = 0;
        static int s_LastSelectionHash = 0;
        static int s_LastActiveContext = 0;
        static DataMode s_LastInspectorDataMode = DataMode.Disabled;
        static Type s_LastSelectedEditorType;

        [InitializeOnLoadMethod]
        static void Init()
        {
            SelectionBridge.DeclareDataModeSupport += OnDeclareDataModeSupport;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnDisplayMixedDataModeWarning;
        }

        static void OnDisplayMixedDataModeWarning(UnityEditor.Editor editor)
        {
            var selectedGameObject = editor.target as GameObject;
            if (selectedGameObject == null)
                return;

            if (EditorApplication.isPlaying || InspectorWindowBridge.GetInspectorWindowDataMode(editor) != DataMode.Mixed)
                return;

            EditorGUILayout.HelpBox(k_MixedDataModeWarningMessage, MessageType.Info);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange is PlayModeStateChange.ExitingPlayMode &&
                (Selection.activeObject is EntitySelectionProxy || Selection.activeContext is EntitySelectionProxy))
                Selection.activeObject = null;

            if (stateChange is not (PlayModeStateChange.EnteredEditMode or PlayModeStateChange.EnteredPlayMode))
                return;
        }

        static void OnDeclareDataModeSupport(UnityObject activeSelection, UnityObject activeContext, HashSet<DataMode> supportedModes)
        {
            // Only claim DOTS inspector support for direct entity selections, not GameObjects
            if (activeSelection is EntitySelectionProxy || activeContext is EntitySelectionProxy)
                AddSupportedDataModes(supportedModes);
        }

        static void AddSupportedDataModes(HashSet<DataMode> supportedDataModes)
        {
            var modes = EditorApplication.isPlaying ? k_RuntimeDataModes : k_EditorDataModes;
            foreach (var mode in modes)
            {
                supportedDataModes.Add(mode);
            }
        }

        [RootEditor(supportsAddComponent : false), UsedImplicitly]
        public static Type GetEditor(UnityObject[] targets, UnityObject context, DataMode inspectorDataMode)
        {
            using var getEditorScope = k_GetEditorMarker.Auto();

            if (targets == null || targets.Length == 0)
            {
                return typeof(InvalidSelectionEditor);
            }

            using var filteredTargetsPool = ListPool<UnityObject>.Get(out var filteredTargetsList);

            // Check if we can use cached editor type based on selection
            using (k_SelectionCompareMarker.Auto())
            {
                FilterOutRemovedEntitiesFromTargets(targets, filteredTargetsList);

                var selectionHash = GetSelectionHash(filteredTargetsList);
                var contextHash = context is null or EntitySelectionProxy { Exists: false } ? 0 : context.GetHashCode();

                // If last editor is unsupported game object editor, we want to reevaluate inspector content
                // even if selection and data mode stay the same.
                if (s_LastSelectedEditorType != typeof(UnsupportedGameObjectEditor) && filteredTargetsList.Count == s_LastSelectionCount && selectionHash == s_LastSelectionHash &&
                    contextHash == s_LastActiveContext && inspectorDataMode == s_LastInspectorDataMode)
                {
                    return s_LastSelectedEditorType;
                }

                s_LastSelectionCount = filteredTargetsList.Count;
                s_LastSelectionHash = selectionHash;
                s_LastActiveContext = contextHash;
                s_LastInspectorDataMode = inspectorDataMode;
            }

            // If not, do the whole editor selection process and cache it
            s_LastSelectedEditorType = SelectEditor(filteredTargetsList, context, inspectorDataMode);
            return s_LastSelectedEditorType;

            static void FilterOutRemovedEntitiesFromTargets(UnityObject[] targets, List<UnityObject> filteredTargets)
            {
                foreach (var target in targets)
                {
                    if (target is null or EntitySelectionProxy { Exists: false })
                        continue;

                    filteredTargets.Add(target);
                }
            }
        }

        static Type SelectEditor([NotNull] List<UnityObject> targets, UnityObject context, DataMode inspectorDataMode)
        {
            using var selectEditorScope = k_SelectEditorMarker.Auto();

            var hasAtLeastOneNonSavableGameObjectTarget = false;
            var hasAtLeastOnePrefabTarget = false;

            foreach (var target in targets)
            {
                // Data mode does not apply to assets.
                if (context is not EntitySelectionProxy && EditorUtility.IsPersistent(target))
                    return null;

                switch (target)
                {
                    case EntitySelectionProxy proxy:
                    {
                        // If a valid EntitySelectionProxy was directly selected
                        // it means we have no backing GameObject, nothing more
                        // is required to provide an inspector. If the proxy was
                        // invalid, however, we can't handle that so we bail.
                        return proxy.Exists
                            ? typeof(EntityEditor)
                            : null;
                    }
                    case GameObject go:
                    {
                        // GameObjects outside of SubScenes cannot save PlayMode changes
                        hasAtLeastOneNonSavableGameObjectTarget = hasAtLeastOneNonSavableGameObjectTarget || !go.scene.isSubScene;

                        // The authoring of GameObjects that are part of prefabs at runtime is not currently supported
                        hasAtLeastOnePrefabTarget = hasAtLeastOnePrefabTarget || PrefabUtility.IsPartOfPrefabAsset(go);
                        break;
                    }
                }
            }

            return inspectorDataMode switch
            {
                // Trying to author a GameObject which can't be saved during PlayMode.
                DataMode.Authoring
                    when EditorApplication.isPlaying &&
                         hasAtLeastOneNonSavableGameObjectTarget
                    => context is EntitySelectionProxy { Exists: true }
                        ? typeof(UnsupportedEntityEditor)
                        : typeof(UnsupportedGameObjectEditor),

                // Trying to author a GameObject that is part of a prefab asset in PlayMode.
                // This is poorly supported at the moment and the UX around it is very confusing,
                // so we're disabling the possibility for the moment.
                DataMode.Authoring
                    when EditorApplication.isPlaying &&
                         hasAtLeastOnePrefabTarget
                    => typeof(UnsupportedPrefabEntityEditor),

                // Inspecting the Runtime representation of a GameObject that is converted/baked into an Entity.
                DataMode.Runtime
                    when context is EntitySelectionProxy { Exists: true }
                    => typeof(EntityEditor),

                DataMode.Runtime
                    when context is EntitySelectionProxy { Exists: false }
                    => typeof(InvalidEntityEditor),

                DataMode.Runtime
                    when context is null && (Selection.activeGameObject != null && Selection.activeGameObject.scene != default && Selection.activeGameObject.scene.isSubScene)
                    => null, // Show default GameObject inspector

                // Anything else: show the default inspector.
                _ => null
            };
        }

        static int GetSelectionHash(List<UnityObject> targets)
        {
            var hash = 0;
            unchecked
            {
                for (var i = 0; i < targets.Count; ++i)
                {
                    if (targets[i] is EntitySelectionProxy { Exists: false })
                        continue;

                    hash = hash * 31 + targets[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
