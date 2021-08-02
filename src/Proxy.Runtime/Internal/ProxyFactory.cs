using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Options;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Assistant.Net.Dynamics.Internal
{
    /// <summary>
    ///     Default dynamic proxy factory implementation based on <see cref="KnownProxy"/> global proxy registry.
    /// </summary>
    internal sealed class ProxyFactory : IProxyFactory
    {
        private readonly ProxyGenerationStrategy strategy;

        public ProxyFactory(IOptions<ProxyFactoryOptions> options)
        {
            strategy = options.Value.Strategy;

            var proxyTypes = options.Value.ProxyTypes;
            var unknownTypes = proxyTypes.Where(x => !KnownProxy.ProxyTypes.Keys.Contains(x)).ToArray();
            if (!unknownTypes.Any())
                return;

            // todo: implement compiled proxy caching
            //var hash = unknownTypes.Aggregate(0, HashCode.Combine);
            //if (File.Exists(ProxyAssemblyLocation(hash)))
            //{
            //    KnownProxy.RegisterFrom(Assembly.LoadFile(ProxyAssemblyLocation(0)));
            //    return;
            //}

            if (strategy != ProxyGenerationStrategy.PrecompiledAndConfigured)
                throw new InvalidOperationException(
                    "Runtime generation wasn't allowed but the following types were configured: "
                    + string.Join(", ", unknownTypes.Select(x => x.FullName)));

            GenerateProxies(unknownTypes);
        }

        Proxy<T> IProxyFactory.Create<T>(T? instance) where T : class
        {
            var type = typeof(T);
            if (!KnownProxy.ProxyTypes.TryGetValue(type, out var proxyTypeImpl))
            {
                if (strategy != ProxyGenerationStrategy.ByRequest)
                    throw new InvalidOperationException($"Proxy generation by request wasn't allowed but the type was requested: {type.FullName}");

                GenerateProxies(type);
                proxyTypeImpl = KnownProxy.ProxyTypes[type];
            }

            return (Proxy<T>?) Activator.CreateInstance(proxyTypeImpl, instance)
                   ?? throw new InvalidOperationException($"Proxy '{proxyTypeImpl}' wasn't created.");
        }

        private static void GenerateProxies(params Type[] proxyTypes)
        {
            Compilation compilation = CSharpCompilation.Create(ProxyAssemblyName)
                .WithOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release));

            foreach (var proxyType in proxyTypes)
                compilation = compilation.AddProxy(proxyType);

            using var memory = new MemoryStream();
            var result = compilation.Emit(memory);
            if (!result.Success)
                throw new InvalidOperationException($"Compilation failed with {result.Diagnostics.Length} errors.");

            memory.Seek(0, SeekOrigin.Begin);
            var rawAssembly = memory.ToArray();

            // todo: implement compiled proxy caching
            //using (var file = File.OpenWrite(ProxyAssemblyLocation(hash)))
            //    file.Write(rawAssembly, 0, rawAssembly.Length);

            KnownProxy.RegisterFrom(Assembly.Load(rawAssembly));
        }

        // todo: implement compiled proxy caching
        //private static string ProxyAssemblyLocation
        //{
        //    get
        //    {
        //        var location = Assembly.GetExecutingAssembly().Location;
        //        var folder = Path.GetDirectoryName(location)!;
        //        return Path.Combine(folder, ProxyAssemblyName);
        //    }
        //}

        private static string ProxyAssemblyName
        {
            get
            {
                var location = Assembly.GetExecutingAssembly().Location;
                var fileName = Path.GetFileNameWithoutExtension(location);
                return fileName + ".proxies.dll";
            }
        }
    }
}