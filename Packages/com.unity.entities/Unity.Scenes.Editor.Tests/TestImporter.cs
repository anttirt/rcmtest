using System.IO;
using UnityEditor.AssetImporters;

namespace Unity.Scenes.Tests
{
    [ScriptedImporter(2, "extDontMatter_TestImporter")]
    internal class TestImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.SetOutputArtifactFile("output", ctx.assetPath);
        }
    }
}
