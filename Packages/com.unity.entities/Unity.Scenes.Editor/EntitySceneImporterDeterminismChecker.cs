using System;
using UnityEditor;
using UnityEditor.Experimental;
using Hash128 = UnityEngine.Hash128;

namespace Unity.Scenes.Editor
{
    /// <summary>
    /// Utility for checking if SubScene imports are deterministic.
    /// </summary>
    public static class EntitySceneImporterDeterminismChecker
    {
        /// <summary>
        /// Contains the result of a determinism check.
        /// </summary>
        public struct Result
        {
            /// <summary>
            /// True if the check completed without errors.
            /// </summary>
            public bool Success;

            /// <summary>
            /// True if both imports produced identical artifact hashes.
            /// </summary>
            public bool IsDeterministic;

            /// <summary>
            /// The artifact hash from the first import.
            /// </summary>
            public Hash128 FirstHash;

            /// <summary>
            /// The artifact hash from the second import.
            /// </summary>
            public Hash128 SecondHash;

            /// <summary>
            /// Error message if Success is false.
            /// </summary>
            public string Error;
        }

        /// <summary>
        /// Checks if a SubScene's import is deterministic by running two imports and comparing artifact hashes.
        /// </summary>
        /// <param name="scenePath">The asset path of the SubScene to check.</param>
        /// <returns>A Result containing the check outcome and artifact hashes.</returns>
        public static Result Check(string scenePath)
        {
            var sceneGuid = AssetDatabaseCompatibility.PathToGUID(scenePath);
            if (sceneGuid.Empty())
                return new Result { Error = $"Scene not found: {scenePath}" };

            var configGUID = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(sceneGuid, default, true, out var createdNew);
            if (createdNew)
                AssetDatabase.Refresh();

            var artifactKey = new ArtifactKey(configGUID, typeof(SubSceneImporter));

            var firstHash = ForceProduceAndGetHash(artifactKey);
            if (!firstHash.isValid)
                return new Result { Error = "First import failed" };

            var secondHash = ForceProduceAndGetHash(artifactKey);
            if (!secondHash.isValid)
                return new Result { Error = "Second import failed" };

            return new Result
            {
                Success = true,
                IsDeterministic = firstHash == secondHash,
                FirstHash = firstHash,
                SecondHash = secondHash
            };
        }

        static Hash128 ForceProduceAndGetHash(ArtifactKey artifactKey)
        {
            AssetDatabaseExperimental.ForceProduceArtifact(artifactKey);
            return AssetDatabaseCompatibility.ProduceArtifact(artifactKey);
        }
    }
}
