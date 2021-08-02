using Assistant.Net.Dynamics.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Assistant.Net.Dynamics
{
    /// <summary>
    ///     Global proxy registry.
    /// </summary>
    public static class KnownProxy
    {
        private static readonly Dictionary<Type, Type> Types = new();

        /// <summary>
        ///     Known proxy type implementations.
        /// </summary>
        public static ImmutableDictionary<Type, Type> ProxyTypes => Types.ToImmutableDictionary();

        /// <summary>
        ///     Registers all proxy type implementations from all referenced assemblies.
        /// </summary>
        [ModuleInitializer]
        internal static void Initialize()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                KnownProxy.RegisterFrom(assembly);
        }

        /// <summary>
        ///     Registers all proxy type implementations from the <paramref name="proxyAssembly"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static void RegisterFrom(Assembly proxyAssembly)
        {
            var proxyTypes = proxyAssembly.GetTypes().Where(x => x.IsProxy());
            foreach (var proxyType in proxyTypes)
                Register(proxyType);
        }

        /// <summary>
        ///     Registers the <paramref name="proxyType"/> implementation.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static bool Register(Type proxyType)
        {
            var instanceType = proxyType.GetInstanceType();
            if (Types.ContainsKey(instanceType))
                return false;

            Types.Add(instanceType, proxyType);
            return true;
        }

        /// <summary>
        ///     Resolves proxy type from proxy type implementation.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public static Type GetInstanceType(this Type proxyType)
        {
            if (!proxyType.IsProxy())
                throw NotProxyTypeError(proxyType);

            return proxyType.BaseType!.GetGenericArguments().Single();
        }

        /// <summary>
        ///     Checks if the <paramref name="type"/> is a proxy.
        /// </summary>
        public static bool IsProxy(this Type type) =>
            typeof(IProxy).IsAssignableFrom(type)
            && type.BaseType is {IsGenericType: true}
            && type.BaseType.GetGenericTypeDefinition() == typeof(Proxy<>);

        private static Exception NotProxyTypeError(Type proxyType) =>
            new InvalidOperationException($"Type '{proxyType}' isn't a proxy.");
    }
}