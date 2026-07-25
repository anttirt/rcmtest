using NUnit.Framework;
using Unity.Scenes.Editor;

namespace Unity.Scenes.Tests
{
    [TestFixture]
    public class EntitySceneImporterDeterminismTests
    {
        [Test]
        public void SubSceneImport_WithTextureDependency_IsDeterministic()
        {
            var result = EntitySceneImporterDeterminismChecker.Check(
                "Packages/com.unity.entities/Unity.Scenes.Editor.Tests/Assets/SceneWithTextureDependency/SubScene.unity");

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.IsDeterministic, $"Non-deterministic: first={result.FirstHash}, second={result.SecondHash}");
        }

        [Test]
        public void SubSceneImport_WithMaterialDependency_IsDeterministic()
        {
            var result = EntitySceneImporterDeterminismChecker.Check(
                "Packages/com.unity.entities/Unity.Scenes.Editor.Tests/Assets/SceneWithMaterialDependency/SubScene.unity");

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.IsDeterministic, $"Non-deterministic: first={result.FirstHash}, second={result.SecondHash}");
        }
    }
}
