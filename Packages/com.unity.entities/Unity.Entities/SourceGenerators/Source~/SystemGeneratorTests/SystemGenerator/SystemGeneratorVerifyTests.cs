using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators
{
    [TestClass]
    public class SystemGeneratorVerifyTests
    {
        [TestMethod]
        public async Task CorrectCodeGenerationWithDirectives()
        {
            const string testSource = @"
namespace MyNamespace
{
#if DEFINE_A
    using Unity.Entities;
#if DEFINE_B
    using Unity.Entities.Tests;
#endif
#endif
#if DEFINE_C // The next `using` directive should not be generated.
    using Unity.Burst;

    public struct DefineCStruct : IComponentData {}
#endif

    public partial struct MySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var data in SystemAPI.Query<RefRO<EcsTestData>>()) {}
        }
    }
}";
            await VerifyCS.VerifySourceGeneratorWithPreprocessorSymbolAsync(
                testSource,
                new []{ "DEFINE_A", "DEFINE_B" },
                nameof(CorrectCodeGenerationWithDirectives),
                "Test0__System_19875963020.g.cs");
        }
    }
}
