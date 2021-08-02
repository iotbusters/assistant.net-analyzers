using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Internal;
using Assistant.Net.Dynamics.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Assistant.Net.Dynamics
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Registers all proxy type implementations from all referenced assemblies.
        ///     This logic performs the role of module initializer if one of extensions was references.
        /// </summary>
        static ServiceCollectionExtensions()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                KnownProxy.RegisterFrom(assembly);
        }

        /// <summary>
        ///     Adds <see cref="IProxyFactory" /> implementation.
        ///     Pay attention, you may need to call explicitly <see cref="ConfigureProxyFactoryOptions" />
        ///     to ensure required proxy types are registered.
        /// </summary>
        public static IServiceCollection AddProxyFactory(this IServiceCollection services)
        {
            services.TryAddSingleton<IProxyFactory, ProxyFactory>();
            return services;
        }

        /// <summary>
        ///     Adds <see cref="IProxyFactory"/> implementation and <see cref="ProxyFactoryOptions"/> configuration.
        /// </summary>
        public static IServiceCollection AddProxyFactory(this IServiceCollection services, Action<ProxyFactoryOptions> configureOptions) => services
            .ConfigureProxyFactoryOptions(configureOptions)
            .AddProxyFactory();

        /// <summary>
        ///     Registers an action used to configure <see cref="ProxyFactoryOptions"/> options.
        /// </summary>
        public static IServiceCollection ConfigureProxyFactoryOptions(this IServiceCollection services, Action<ProxyFactoryOptions> configureOptions) => services
            .Configure(configureOptions);
    }
}