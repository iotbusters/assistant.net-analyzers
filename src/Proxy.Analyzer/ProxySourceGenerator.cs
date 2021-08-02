using Assistant.Net.Dynamics.Builders;
using Microsoft.CodeAnalysis;

namespace Assistant.Net.Dynamics.Analyzers
{
    /// <summary>
    ///     Proxy source code generator.
    /// </summary>
    /// <seealso cref="https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md"/>
    [Generator]
    public class ProxySourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) =>
            context.RegisterForSyntaxNotifications(() => new ProxyFactoryCreateReceiver());

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not ProxyFactoryCreateReceiver receiver)
                return;

            var builder = new SourceBuilder();

            var proxyTypes = receiver.ProxyTypes;
            foreach (var proxyType in proxyTypes)
                context.Compilation.GenerateProxy(builder, proxyType);

            context.AddSource("proxies.g.cs", builder.ToString());
        }
    }
}