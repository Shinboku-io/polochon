using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Polochon.SourceGenerator
{
    [Generator]
    public sealed class ModuleMediatorBridgeGenerator : IIncrementalGenerator
    {
        private const string ModuleBaseMetadataName = "Polochon.Modules.ModuleBase";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<INamedTypeSymbol?> moduleTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                    transform: static (syntaxContext, cancellationToken) => GetModuleType(syntaxContext, cancellationToken))
                .Where(static symbol => symbol is not null);

            context.RegisterSourceOutput(moduleTypes.Collect(), static (spc, modules) =>
            {
                foreach (INamedTypeSymbol module in modules.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
                {
                    spc.AddSource(GetHintName(module), SourceText.From(GenerateSource(module), Encoding.UTF8));
                }
            });
        }

        private static INamedTypeSymbol? GetModuleType(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.Node is not ClassDeclarationSyntax classDeclarationSyntax)
            {
                return null;
            }

            if (context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return null;
            }

            if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
            {
                return null;
            }

            return DerivesFromModuleBase(typeSymbol) ? typeSymbol : null;
        }

        private static bool DerivesFromModuleBase(INamedTypeSymbol typeSymbol)
        {
            for (INamedTypeSymbol? current = typeSymbol.BaseType; current is not null; current = current.BaseType)
            {
                if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + ModuleBaseMetadataName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GenerateSource(INamedTypeSymbol moduleType)
        {
            var source = new StringBuilder();
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using System.Linq;");
            source.AppendLine("using System.Reflection;");
            source.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            source.AppendLine();

            bool hasNamespace = !moduleType.ContainingNamespace.IsGlobalNamespace;
            string namespaceName = hasNamespace ? moduleType.ContainingNamespace.ToDisplayString() : string.Empty;

            if (hasNamespace)
            {
                source.Append("namespace ").Append(namespaceName).AppendLine();
                source.AppendLine("{");
            }

            source.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Polochon.SourceGenerator\", \"1.0.0\")]");
            source.Append("    internal static class ").Append(moduleType.Name).AppendLine("MediatorConfiguration");
            source.AppendLine("    {");
            source.AppendLine("        internal static void ConfigureMediatorServices(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Collections.Generic.IReadOnlyList<global::System.Reflection.Assembly> types)");
            source.AppendLine("        {");
            source.AppendLine("            _ = services.AddMediator((global::Mediator.MediatorOptions options) =>");
            source.AppendLine("            {");
            source.AppendLine("                options.GenerateTypesAsInternal = true;");
            source.AppendLine("                options.ServiceLifetime = global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton;");
            source.AppendLine("                options.Assemblies = types.Select(x => (global::Mediator.AssemblyReference)x).ToList().AsReadOnly();");
            source.AppendLine("            });");
            source.AppendLine("        }");
            source.AppendLine("    }");

            if (hasNamespace)
            {
                source.AppendLine("}");
            }

            return source.ToString();
        }

        private static string GetHintName(INamedTypeSymbol moduleType)
            => $"{moduleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty).Replace('.', '_')}_MediatorBridge.g.cs";
    }
}
