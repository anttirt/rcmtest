using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TypeAnalyzer : DiagnosticAnalyzer
    {
        const string k_ManagedSharedComponentSilenceDefine = "UNITY_DISABLE_MANAGED_SHARED_COMPONENT_WARNINGS";
        const string k_ISharedComponentDataFullName = "global::Unity.Entities.ISharedComponentData";

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.StructDeclaration, SyntaxKind.ClassDeclaration);
        }

        static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            var typeDeclaration = context.Node as TypeDeclarationSyntax;
            Debug.Assert(typeDeclaration != null, nameof(typeDeclaration) + " != null");
            if (typeDeclaration.BaseList == null || typeDeclaration.BaseList.Types.Count == 0)
                return;

            if (typeDeclaration is StructDeclarationSyntax structDeclaration)
                AnalyzeManagedSharedComponent(context, structDeclaration);

            // Error on missing IJobEntity
            foreach (var type in typeDeclaration.BaseList.Types)
                if (type.Type is IdentifierNameSyntax { Identifier: { ValueText: "IJobEntity" } })
                {
                    var declaredType = context.SemanticModel.GetTypeInfo(type.Type).Type;
                    var fullName = declaredType.ToFullName();
                    if (fullName is not ("global::Unity.Entities.IJobEntity"))
                        continue;

                    for (var parent = typeDeclaration.Parent; parent is TypeDeclarationSyntax parentType; parent = parent.Parent)
                    {
                        // If we have partial continue to next parent
                        foreach (var modifier in parentType.Modifiers)
                            if (modifier.IsKind(SyntaxKind.PartialKeyword))
                                goto NextParent;

                        var declaredInnerSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                        var declaredParentSymbol = context.SemanticModel.GetDeclaredSymbol(parentType);
                        context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0008Descriptor, parentType.Identifier.GetLocation(), type.Type, declaredInnerSymbol.ToFullName(), declaredParentSymbol.ToFullName()));
                        NextParent:;
                    }

                    foreach (var modifier in typeDeclaration.Modifiers)
                        if (modifier.IsKind(SyntaxKind.PartialKeyword))
                            return;

                    var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0007Descriptor, typeDeclaration.Identifier.GetLocation(), type.Type, declaredSymbol.ToFullName()));
                    return;
                }

            foreach (var modifier in typeDeclaration.Modifiers)
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    return;

            // Error on missing System
            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration); // Because of SystemBase supporting inheritance
            var (isSystem, systemType) = typeSymbol.TryGetSystemType();
            if (isSystem)
                context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0007Descriptor, typeDeclaration.Identifier.GetLocation(), systemType.ToString(), typeSymbol.ToFullName()));
        }

        static void AnalyzeManagedSharedComponent(SyntaxNodeAnalysisContext context, StructDeclarationSyntax structDeclaration)
        {
            if (context.Node.SyntaxTree.Options is CSharpParseOptions opts)
            {
                foreach (var symbol in opts.PreprocessorSymbolNames)
                    if (symbol == k_ManagedSharedComponentSilenceDefine)
                        return;
            }

            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(structDeclaration);
            if (typeSymbol == null)
                return;

            var implementsSharedComponent = false;
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (iface.ToFullName() == k_ISharedComponentDataFullName)
                {
                    implementsSharedComponent = true;
                    break;
                }
            }
            if (!implementsSharedComponent)
                return;

            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IFieldSymbol field
                    && !field.IsStatic
                    && !field.IsConst
                    && !field.Type.IsUnmanagedType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        EntitiesDiagnostics.k_Ea0017Descriptor,
                        structDeclaration.Identifier.GetLocation(),
                        typeSymbol.ToFullName()));
                    return;
                }
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0007Descriptor, EntitiesDiagnostics.k_Ea0008Descriptor,
            EntitiesDiagnostics.k_Ea0017Descriptor);
    }
}
