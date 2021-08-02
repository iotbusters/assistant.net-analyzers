using Assistant.Net.Dynamics.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     IProxyFactory usage analysis visitor.
    /// </summary>
    /// <seealso cref="https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md"/>
    /// <seealso cref="https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm"/>
    public class ProxyFactoryCreateReceiver : ISyntaxContextReceiver
    {
        public HashSet<INamedTypeSymbol> ProxyTypes { get; } = new(SymbolEqualityComparer.Default);

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is not InvocationExpressionSyntax s)
                return;

            var createProxySymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(IProxyFactory).FullName!)!
                .GetMembers().Single(_ => _.Name == nameof(IProxyFactory.Create));
            //var createProxySymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("Assistant.Net.Dynamics.Abstractions.IProxyFactory")!
            //    .GetMembers().Single(_ => _.Name == "Create");

            var nodeSymbol = context.SemanticModel.GetSymbolInfo(context.Node);
            if(nodeSymbol.Symbol is not IMethodSymbol invocationSymbol)
                return;

            if (!SymbolEqualityComparer.Default.Equals(createProxySymbol, invocationSymbol.ConstructedFrom))
                return;

            var typeSymbol = invocationSymbol.TypeArguments[0];
            ProxyTypes.Add((INamedTypeSymbol)typeSymbol);
        }
    }
}