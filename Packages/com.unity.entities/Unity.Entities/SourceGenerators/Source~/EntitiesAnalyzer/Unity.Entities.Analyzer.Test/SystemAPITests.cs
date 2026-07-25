using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.SystemAPIAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class SystemAPITests
    {
        [DataTestMethod]
        [DataRow("HasSingleton", @"{|#0:SystemAPI.HasSingleton<EcsTestData>()|}")]
        [DataRow("HasSingleton", @"{|#0:HasSingleton<EcsTestData>()|}")]
        [DataRow("Time", @"var time = {|#0:SystemAPI.Time|}.DeltaTime")]
        [DataRow("Time", @"var time = {|#0:Time|}.DeltaTime")]
        public async Task SystemAPIUseInNonSystemType_Error(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class NonSystemType
                {{
                    protected void OnUpdate()
                    {{
                        {apiMethodInvocation};
                    }}
                }}";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0004).WithLocation(0).WithArguments(memberName);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [DataTestMethod]
        [DataRow("HasSingleton", @"{|#0:SystemAPI.HasSingleton<EcsTestData>()|}")]
        [DataRow("HasSingleton", @"{|#0:HasSingleton<EcsTestData>()|}")]
        [DataRow("Time", @"var time = {|#0:SystemAPI.Time|}.DeltaTime")]
        [DataRow("Time", @"var time = {|#0:Time|}.DeltaTime")]
        public async Task SystemAPIUseInStaticMethod_Error(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial struct TestSystem : ISystem
                {{
                    static void StaticMethod()
                    {{
                        {apiMethodInvocation};
                    }}
                }}";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0006).WithLocation(0).WithArguments(memberName);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
