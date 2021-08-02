using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Builders;

namespace Assistant.Net.Dynamics
{
    public static class CompilationExtensions
    {
        /// <summary>
        ///     Adds a generated proxy for <typeparamref name="T"/> interface to new compilation.
        /// </summary>
        public static Compilation AddProxy<T>(this Compilation compilation, string? @namespace = null) where T : class =>
            compilation.AddProxy(typeof(T), @namespace);

        /// <summary>
        ///     Adds a generated proxy from type to new compilation.
        /// </summary>
        public static Compilation AddProxy(this Compilation compilation, Type proxyType, string? @namespace = null)
        {
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var systemAssemblies = new[]
            {
                Path.Combine(assemblyPath, "System.Runtime.dll"),
                Path.Combine(assemblyPath, "netstandard.dll"),
                proxyType.Assembly.Location
            }.Distinct().Select(x => MetadataReference.CreateFromFile(x));
            compilation = compilation.AddReferences(systemAssemblies); 

            var proxyTypeSymbol = compilation.GetTypeSymbol(proxyType);
            return compilation.AddProxy(proxyTypeSymbol, @namespace);
        }

        /// <summary>
        ///     Adds a generated proxy from type symbol to new compilation.
        /// </summary>
        public static Compilation AddProxy(this Compilation compilation, INamedTypeSymbol proxyType, string? @namespace = null)
        {
            var builder = new SourceBuilder();
            return compilation
                .GenerateProxy(builder, proxyType, @namespace)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(builder.ToString()));
        }

        /// <summary>
        ///     Generates a proxy in source builder and adds dependencies to new compilation.
        /// </summary>
        public static Compilation GenerateProxy(this Compilation compilation, SourceBuilder builder, INamedTypeSymbol proxyType, string? @namespace = null)
        {
            if (proxyType.TypeKind != TypeKind.Interface)
                throw new ArgumentException(
                    $"Expected an interface type but provided `{proxyType.Name}` instead.",
                    proxyType.Name);

            var systemAssemblies = new[] {typeof(Proxy<>).Assembly.Location}.Select(x => MetadataReference.CreateFromFile(x));
            compilation = compilation.AddReferences(systemAssemblies);

            var baseProxyTypeDefinition = compilation.GetTypeSymbol(typeof(Proxy<>));
            if(baseProxyTypeDefinition == null)
                throw new ArgumentException(
                    "Package `assistant.net.dynamics.proxy` is required. Please ensure it was installed.",
                    proxyType.Name);

            var defaultNamespace = proxyType.ContainingNamespace.ToString();
            var localAssemblies = new[]
                {
                    typeof(Func<>).Assembly.Location,
                    //typeof(Proxy<>).Assembly.Location,
                    typeof(Exception).Assembly.Location,
                    typeof(Enumerable).Assembly.Location,
                    typeof(MethodInfo).Assembly.Location
                }.Distinct().Select(x => MetadataReference.CreateFromFile(x));

            compilation = compilation.AddReferences(localAssemblies);

            var proxyTypeName = proxyType.Name + "Proxy";
            //var baseProxyType = compilation.GetTypeSymbol(typeof(Proxy<>))!.Construct(proxyType);
            var baseProxyType = baseProxyTypeDefinition!.Construct(proxyType);
            // Source generator couldn't resolve type symbols for some reason.
            // assuming, multiple types are being resolved because framework mixture.
            var exceptionType = "System.Exception";//compilation.GetTypeSymbol(typeof(Exception))!;
            var methodInfoType = "System.Reflection.MethodInfo";//compilation.GetTypeSymbol(typeof(MethodInfo))!;

            builder.AddNamespace(@namespace ?? defaultNamespace, nb =>
            {
                nb.AddClass(proxyTypeName, new []{ baseProxyType, proxyType }, cb =>
                {
                    var proxyTypeProperties = proxyType.GetMembers().OfType<IPropertySymbol>().ToArray();
                    var proxyTypeMethods = proxyType.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary).ToArray();
                    var proxyTypeEvents = proxyType.GetMembers().OfType<IEventSymbol>().ToArray();
                    var instanceFieldName = "instance";
                    var errorFieldName = "interceptionFailure";

                    cb.AddField(proxyType, instanceFieldName);
                    cb.AddField(exceptionType, errorFieldName);

                    foreach (var property in proxyTypeProperties)
                    {
                        if (property.GetMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("get", property.Name));
                        if (property.SetMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("set", property.Name));
                    }

                    foreach (var method in proxyTypeMethods)
                        cb.AddField(methodInfoType, buildName: b => b.MethodName(method));

                    foreach (var @event in proxyTypeEvents)
                    {
                        if (@event.AddMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("add", @event.Name));
                        if (@event.RemoveMethod != null)
                            cb.AddField(methodInfoType, buildName: b => b.Append("remove", @event.Name));
                    }

                    cb.AddCtor(new[] {(instanceFieldName, proxyType)}, ccb =>
                    {
                        ccb.AppendLine("this.", instanceFieldName, " = ", instanceFieldName, ";");
                        ccb.AppendLine("this.", errorFieldName, " = ")
                            .Append("new InvalidOperationException(\"Neither ", instanceFieldName, " field was set")
                            .AppendLine(" nor interception configured.\");");

                        foreach (var property in proxyTypeProperties)
                        {
                            if (property.GetMethod != null)
                                ccb.Append("this.get", property.Name, " = typeof(").Type(proxyType).Append(")")
                                    .AppendLine(".GetProperty(\"", property.Name, "\").GetMethod;");
                            if (property.SetMethod != null)
                                ccb.Append("this.set", property.Name, " = typeof(").Type(proxyType).Append(")")
                                    .AppendLine(".GetProperty(\"", property.Name, "\").SetMethod;");
                        }

                        foreach (var method in proxyTypeMethods)
                        {
                            var parameterTypes = method.Parameters.Select(x => x.Type).ToArray();
                            if (method.IsGenericMethod && parameterTypes.Any())
                                ccb.Append("this.").MethodName(method).Append(" = typeof(").Type(proxyType)
                                    .Append(").GetMethods()")
                                    .Append(".Single(x => x.IsGenericMethod && x.Name == \"").Append(method.Name).Append("\" ")
                                    .Append("&& x.GetParameters().Select(y => y.ParameterType.Name).SequenceEqual(new string[] {")
                                    .AppendJoin(", ", parameterTypes, (b, type) => b.Append("\"", type.Name, "\""))
                                    .AppendLine("}));");
                            else
                                ccb.Append("this.").MethodName(method).Append(" = typeof(").Type(proxyType)
                                    .Append(").GetMethod(\"")
                                    .Append(method.Name).Append("\", new Type[] {")
                                    .AppendJoin(
                                        ", ",
                                        parameterTypes,
                                        (b, type) => b.Append("typeof(").Type(type).Append(")"))
                                    .AppendLine("});");
                        }

                        foreach (var @event in proxyTypeEvents)
                        {
                            if (@event.AddMethod != null)
                                ccb.Append("this.add", @event.Name, " = typeof(").Type(proxyType)
                                    .AppendLine(").GetEvent(\"", @event.Name, "\").AddMethod;");
                            if (@event.RemoveMethod != null)
                                ccb.Append("this.remove", @event.Name, " = typeof(").Type(proxyType)
                                    .AppendLine(").GetEvent(\"", @event.Name, "\").RemoveMethod;");
                        }
                    });

                    foreach (var property in proxyTypeProperties)
                        cb.AddProperty(
                            property,
                            getter: b => b
                                .Append("var interceptor = GetImplementation(").AppendLine("this.get", property.Name, ");")
                                .Append("if (interceptor != null) ")
                                .Append("return (").Type((INamedTypeSymbol) property.Type).AppendLine(") interceptor(new object[0]);")
                                .Append("if (", instanceFieldName, " != null) ")
                                .AppendLine("return ", instanceFieldName, ".", property.Name, ";")
                                .AppendLine("throw this.", errorFieldName, ";"),
                            setter: b => b
                                .Append("var interceptor = GetImplementation(").AppendLine("this.set", property.Name, ");")
                                .AppendLine("if (interceptor != null) ")
                                .AddBlock(ib => ib
                                    .AppendLine("interceptor(new object[] {value});")
                                    .AppendLine("return;"))
                                .AppendLine("if (", instanceFieldName, " != null)")
                                .AddBlock(ib => ib
                                    .AppendLine("this.", instanceFieldName, ".", property.Name, " = value;")
                                    .AppendLine("return;"))
                                .AppendLine("throw this.", errorFieldName, ";")
                            );

                    foreach (var method in proxyTypeMethods)
                    {
                        var argumentNames = method.Parameters.Select(x => x.Name!).ToArray();
                        cb.AddMethod(method, b =>
                        {
                            b.Append("var interceptor = GetImplementation(");
                            if (method.IsGenericMethod)
                                b.Append("this.").MethodName(method)
                                    .Append(".MakeGenericMethod(")
                                    .AppendJoin(", ", method.TypeArguments.ToArray(), (bt, type) => bt.Append("typeof(").Type(type).Append(")"))
                                    .Append(")");
                            else
                                b.Append("this.").MethodName(method);
                            b.AppendLine(");");

                            if (method.ReturnsVoid)
                                b.AppendLine("if (interceptor != null) ")
                                    .AddBlock(ib => ib
                                        .Append("interceptor(new object[] {")
                                        .AppendJoin(", ", argumentNames).AppendLine("});")
                                        .AppendLine("return;"))
                                    .AppendLine("if (", instanceFieldName, " != null)")
                                    .AddBlock(ib => ib
                                        .Append("this.", instanceFieldName, ".", method.Name, "(")
                                        .AppendJoin(", ", argumentNames).AppendLine(");")
                                        .AppendLine("return;"))
                                    .AppendLine("throw this.", errorFieldName, ";");
                            else
                                b.Append("if (interceptor != null) ")
                                    .Append("return (").Type(method.ReturnType).Append(") interceptor((new object[] {")
                                    .AppendJoin(", ", argumentNames).AppendLine("}));")
                                    .Append("if (", instanceFieldName, " != null) ")
                                    .Append("return this.", instanceFieldName, ".", method.Name, "(")
                                    .AppendJoin(", ", argumentNames).AppendLine(");")
                                    .AppendLine("throw this.", errorFieldName, ";");
                        });
                    }

                    foreach (var @event in proxyTypeEvents)
                    {
                        cb.AddEvent(
                            @event,
                            addBody: b => b
                                .AppendLine("var interceptor = GetImplementation(this.add", @event.Name, ");")
                                .AppendLine("if (interceptor != null) ")
                                .AddBlock(ib => ib
                                    .AppendLine("interceptor(new object[] {value});")
                                    .AppendLine("return;"))
                                .Append("if (", instanceFieldName, " != null) ")
                                .AppendLine("this.", instanceFieldName, ".", @event.Name, " += value;")
                                .Append("throw this.", errorFieldName, ";"),
                            removeBody: b => b
                                .AppendLine("var interceptor = GetImplementation(this.add", @event.Name, ");")
                                .AppendLine("if (interceptor != null) ")
                                .AddBlock(ib => ib
                                    .AppendLine("interceptor(new object[] {value});")
                                    .AppendLine("return;"))
                                .Append("if (", instanceFieldName, " != null) ")
                                .AppendLine("this.", instanceFieldName, ".", @event.Name, " -= value;")
                                .AppendLine("throw this.", errorFieldName, ";"));
                    }
                });
            });

            return compilation;
        }

        private static INamedTypeSymbol GetTypeSymbol(this Compilation compilation, Type proxyType) =>
            compilation.GetTypeByMetadataName(proxyType.FullName!)
            ?? throw new InvalidOperationException($"Cannot find a symbol for '{proxyType}' type.");
    }
}