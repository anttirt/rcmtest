#if UNITY_DOTS_IMHEX
using System;
using NUnit.Framework;
using TypeHelper = Unity.Entities.ImHexPatternEntitySceneBinaryWriter.TypeHelper;

namespace Unity.Entities.Editor.Tests.Serialization
{
    [TestFixture]
    class ImHexPatternWriterTests
    {
        static string WriteAndGetOutput(Action<ImHexPatternEntitySceneBinaryWriter> writeAction)
        {
            var writer = ImHexPatternEntitySceneBinaryWriter.Create("Test");
            writer.shouldWriteToDisk = false;
            writeAction(writer);
            var output = writer.GetOutput();
            writer.Dispose();
            return output;
        }

        [Test]
        public void GetImHexSupportedName_ReplacesSpecialCharacters()
        {
            Assert.AreEqual("Unity_Entities_Entity", TypeHelper.GetImHexSupportedName("Unity.Entities.Entity"));
            Assert.AreEqual("Outer_Inner", TypeHelper.GetImHexSupportedName("Outer+Inner"));
            Assert.AreEqual("Generic_1", TypeHelper.GetImHexSupportedName("Generic`1"));
            Assert.AreEqual("List_Int32_", TypeHelper.GetImHexSupportedName("List<Int32>"));
            Assert.AreEqual("IntPtr", TypeHelper.GetImHexSupportedName("IntPtr*"));
        }

        [Test]
        public void GetImHexSupportedName_StripsArrayAndAssemblyQualifiers()
        {
            Assert.AreEqual("System_Int32", TypeHelper.GetImHexSupportedName("System.Int32, mscorlib"));
            Assert.AreEqual("System_Int32", TypeHelper.GetImHexSupportedName("System.Int32[]"));
            Assert.AreEqual("System_Int32", TypeHelper.GetImHexSupportedName("System.Int32[,]"));
        }

        [Test]
        public void GetImHexSupportedName_EscapesReservedKeywords()
        {
            Assert.AreEqual("in_", TypeHelper.GetImHexSupportedName("in"));
            Assert.AreEqual("out_", TypeHelper.GetImHexSupportedName("out"));
            Assert.AreEqual("ref_", TypeHelper.GetImHexSupportedName("ref"));
            Assert.AreEqual("type_", TypeHelper.GetImHexSupportedName("type"));
            Assert.AreEqual("match_", TypeHelper.GetImHexSupportedName("match"));
            Assert.AreEqual("struct_", TypeHelper.GetImHexSupportedName("struct"));
            Assert.AreEqual("enum_", TypeHelper.GetImHexSupportedName("enum"));
            Assert.AreEqual("union_", TypeHelper.GetImHexSupportedName("union"));
            Assert.AreEqual("namespace_", TypeHelper.GetImHexSupportedName("namespace"));
            Assert.AreEqual("return_", TypeHelper.GetImHexSupportedName("return"));
            Assert.AreEqual("if_", TypeHelper.GetImHexSupportedName("if"));
            Assert.AreEqual("else_", TypeHelper.GetImHexSupportedName("else"));
            Assert.AreEqual("while_", TypeHelper.GetImHexSupportedName("while"));
            Assert.AreEqual("for_", TypeHelper.GetImHexSupportedName("for"));
            Assert.AreEqual("break_", TypeHelper.GetImHexSupportedName("break"));
            Assert.AreEqual("continue_", TypeHelper.GetImHexSupportedName("continue"));
            Assert.AreEqual("true_", TypeHelper.GetImHexSupportedName("true"));
            Assert.AreEqual("false_", TypeHelper.GetImHexSupportedName("false"));
            Assert.AreEqual("null_", TypeHelper.GetImHexSupportedName("null"));
            Assert.AreEqual("this_", TypeHelper.GetImHexSupportedName("this"));
            Assert.AreEqual("parent_", TypeHelper.GetImHexSupportedName("parent"));
            Assert.AreEqual("using_", TypeHelper.GetImHexSupportedName("using"));
            Assert.AreEqual("import_", TypeHelper.GetImHexSupportedName("import"));
            Assert.AreEqual("from_", TypeHelper.GetImHexSupportedName("from"));
            Assert.AreEqual("as_", TypeHelper.GetImHexSupportedName("as"));
            Assert.AreEqual("fn_", TypeHelper.GetImHexSupportedName("fn"));
            Assert.AreEqual("let_", TypeHelper.GetImHexSupportedName("let"));
            Assert.AreEqual("be_", TypeHelper.GetImHexSupportedName("be"));
            Assert.AreEqual("le_", TypeHelper.GetImHexSupportedName("le"));
            Assert.AreEqual("bitfield_", TypeHelper.GetImHexSupportedName("bitfield"));
            Assert.AreEqual("addr_", TypeHelper.GetImHexSupportedName("addr"));
            Assert.AreEqual("addressof_", TypeHelper.GetImHexSupportedName("addressof"));
        }

        [Test]
        public void GetImHexSupportedName_DoesNotEscapeNonKeywords()
        {
            Assert.AreEqual("MyType", TypeHelper.GetImHexSupportedName("MyType"));
            Assert.AreEqual("Entity", TypeHelper.GetImHexSupportedName("Entity"));
            Assert.AreEqual("input", TypeHelper.GetImHexSupportedName("input"));
            Assert.AreEqual("output", TypeHelper.GetImHexSupportedName("output"));
            Assert.AreEqual("refcount", TypeHelper.GetImHexSupportedName("refcount"));
            Assert.AreEqual("typeIndex", TypeHelper.GetImHexSupportedName("typeIndex"));
        }

        [Test]
        public void GetImHexSupportedFullName_WorksWithActualTypes()
        {
            Assert.AreEqual("System_Int32", TypeHelper.GetImHexSupportedFullName(typeof(int)));
            Assert.AreEqual("System_String", TypeHelper.GetImHexSupportedFullName(typeof(string)));
            Assert.AreEqual("Unity_Entities_Entity", TypeHelper.GetImHexSupportedFullName(typeof(Entity)));
        }

        [Test]
        public void IsChunkComponent_IdentifiesChunkBufferType()
        {
            Assert.IsFalse(TypeHelper.IsChunkComponent(typeof(Entity)));
            Assert.IsFalse(TypeHelper.IsChunkComponent(typeof(int)));
        }

        [Test]
        public void IsFakeGeneric_IdentifiesBacktickedTypes()
        {
            Assert.IsFalse(TypeHelper.IsFakeGeneric(typeof(Entity)));
            Assert.IsFalse(TypeHelper.IsFakeGeneric(typeof(int)));
        }

        enum TestEnumWithReservedKeywords
        {
            Normal,
            @in,
            @out,
            @ref,
            @type
        }

        [Test]
        public void Writer_EscapesReservedKeywordsInEnumMembers()
        {
            var output = WriteAndGetOutput(w => w.WriteTypeWithPosition<TestEnumWithReservedKeywords>("testEnum", 0));

            Assert.That(output, Does.Contain("in_,"));
            Assert.That(output, Does.Contain("out_,"));
            Assert.That(output, Does.Contain("ref_,"));
            Assert.That(output, Does.Contain("type_"));
            Assert.That(output, Does.Contain("Normal,"));
        }

        struct SimpleTestStruct
        {
            public int value;
            public float position;
        }

        [Test]
        public void Writer_WritesStructWithFields()
        {
            var output = WriteAndGetOutput(w => w.WriteTypeWithPosition<SimpleTestStruct>("testStruct", 0));

            Assert.That(output, Does.Contain("struct"));
            Assert.That(output, Does.Contain("SimpleTestStruct"));
            Assert.That(output, Does.Contain("value"));
            Assert.That(output, Does.Contain("position"));
            Assert.That(output, Does.Contain("s32"));
            Assert.That(output, Does.Contain("float"));
        }

        [Test]
        public void Writer_WritesArrayWithElementCount()
        {
            var output = WriteAndGetOutput(w => w.WriteArrayOfTypeWithPosition<SimpleTestStruct>("testArray", 0, 10));

            Assert.That(output, Does.Contain("testArray[10]"));
            Assert.That(output, Does.Contain("@ 0"));
        }

        struct NestedTestStruct
        {
            public SimpleTestStruct inner;
            public int count;
        }

        [Test]
        public void Writer_WritesForwardDeclarationsBeforeDefinitions()
        {
            var output = WriteAndGetOutput(w => w.WriteTypeWithPosition<NestedTestStruct>("testNested", 0));

            var usingIndex = output.IndexOf("using");
            var structIndex = output.IndexOf("struct");
            Assert.That(usingIndex, Is.GreaterThan(-1), "Should have forward declaration");
            Assert.That(structIndex, Is.GreaterThan(-1), "Should have struct definition");
            Assert.That(usingIndex, Is.LessThan(structIndex), "Forward declaration should come before struct definition");
        }

        [Test]
        public void Writer_DeduplicatesTypes()
        {
            var output = WriteAndGetOutput(w =>
            {
                w.WriteTypeWithPosition<SimpleTestStruct>("first", 0);
                w.WriteTypeWithPosition<SimpleTestStruct>("second", 100);
            });

            var firstStructIndex = output.IndexOf("struct");
            var lastStructIndex = output.LastIndexOf("struct");
            Assert.That(firstStructIndex, Is.EqualTo(lastStructIndex), "Struct should only be defined once");
            Assert.That(output, Does.Contain("first @"));
            Assert.That(output, Does.Contain("second @"));
        }

        [Test]
        public void Writer_WritesCorrectPositions()
        {
            var output = WriteAndGetOutput(w =>
            {
                w.WriteTypeWithPosition<int>("pos0", 0);
                w.WriteTypeWithPosition<float>("pos4", 4);
                w.WriteTypeWithPosition<SimpleTestStruct>("pos8", 8);
            });

            Assert.That(output, Does.Contain("pos0 @ 0"));
            Assert.That(output, Does.Contain("pos4 @ 4"));
            Assert.That(output, Does.Contain("pos8 @ 8"));
        }

        struct StructWithKeywordFields
        {
            public int @in;
            public float @type;
            public byte @out;
        }

        [Test]
        public void Writer_EscapesReservedKeywordsInFieldNames()
        {
            var output = WriteAndGetOutput(w => w.WriteTypeWithPosition<StructWithKeywordFields>("test", 0));

            Assert.That(output, Does.Contain("in_"));
            Assert.That(output, Does.Contain("type_"));
            Assert.That(output, Does.Contain("out_"));
        }

        struct OuterStruct
        {
            public MiddleStruct middle;
        }

        struct MiddleStruct
        {
            public SimpleTestStruct inner;
        }

        [Test]
        public void Writer_HandlesDeepNesting()
        {
            var output = WriteAndGetOutput(w => w.WriteTypeWithPosition<OuterStruct>("outer", 0));

            Assert.That(output, Does.Contain("OuterStruct"));
            Assert.That(output, Does.Contain("MiddleStruct"));
            Assert.That(output, Does.Contain("SimpleTestStruct"));
        }

        [Test]
        public void Writer_IntegrationTest()
        {
            var output = WriteAndGetOutput(w =>
            {
                w.WriteTypeWithPosition<TestEnumWithReservedKeywords>("enumVal", 0);
                w.WriteTypeWithPosition<StructWithKeywordFields>("keywordFields", 4);
                w.WriteTypeWithPosition<OuterStruct>("nested", 20);
                w.WriteArrayOfTypeWithPosition<SimpleTestStruct>("array", 100, 5);
            });

            Assert.That(output, Does.Contain("in_,"));
            Assert.That(output, Does.Contain("type_"));
            Assert.That(output, Does.Contain("OuterStruct"));
            Assert.That(output, Does.Contain("MiddleStruct"));
            Assert.That(output, Does.Contain("array[5]"));
            var usingIndex = output.IndexOf("using");
            var structIndex = output.IndexOf("struct");
            Assert.That(usingIndex, Is.LessThan(structIndex));
        }

        [Test]
        public void ReleaseOwnership_PreventsDisposeFromThrowingOnSecondCall()
        {
            var writer = ImHexPatternEntitySceneBinaryWriter.Create("Test");
            writer.shouldWriteToDisk = false;

            var copy = writer;
            writer.ReleaseOwnership();
            writer.Dispose();

            copy.Dispose();
        }

        [Test]
        public void ReleaseOwnership_AllowsTransferredWriterToContinueWorking()
        {
            var source = ImHexPatternEntitySceneBinaryWriter.Create("Test");
            source.shouldWriteToDisk = false;

            var destination = source;
            source.ReleaseOwnership();

            destination.WriteTypeWithPosition<SimpleTestStruct>("afterTransfer", 0);
            var output = destination.GetOutput();
            destination.Dispose();

            Assert.That(output, Does.Contain("afterTransfer"));
            Assert.That(output, Does.Contain("SimpleTestStruct"));
        }

        [Test]
        public void Dispose_SetsWriterToNull_PreventsDoubleDispose()
        {
            var writer = ImHexPatternEntitySceneBinaryWriter.Create("Test");
            writer.shouldWriteToDisk = false;

            writer.Dispose();
            Assert.DoesNotThrow(() => writer.Dispose());
        }
    }
}
#endif
