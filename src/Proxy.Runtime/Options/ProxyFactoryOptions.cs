using System;
using System.Collections.Generic;
using System.Linq;

namespace Assistant.Net.Dynamics.Options
{
    /// <summary>
    ///     Required proxy configuration.
    /// </summary>
    public class ProxyFactoryOptions
    {
        private readonly HashSet<Type> proxyTypes = new();

        /// <summary>
        ///     Is generating proxy during the runtime allowed?
        /// </summary>
        internal ProxyGenerationStrategy Strategy { get; set; } = ProxyGenerationStrategy.PrecompiledAndConfigured;

        /// <summary>
        ///     Allows configured proxy generation during the runtime only. It's a default behavior.
        ///     Pay attention, it may impact performance slightly during startup time.
        /// </summary>
        public ProxyFactoryOptions AllowConfiguredOnlyGeneration()
        {
            Strategy = ProxyGenerationStrategy.PrecompiledAndConfigured;
            return this;
        }

        /// <summary>
        ///     Disallows any generating proxy during the runtime, only those which have been precompiled or referenced.
        ///     Pay attention, if some types were configured, it will be fail during the runtime.
        /// </summary>
        public ProxyFactoryOptions AllowPrecompiledOnly()
        {
            Strategy = ProxyGenerationStrategy.Precompiled;
            return this;
        }

        /// <summary>
        ///     Allows generating unknown proxies during the runtime by request.
        ///     Pay attention, it may impact greatly your performance and it makes sense only if proxy types aren't known during compile time.
        /// </summary>
        public ProxyFactoryOptions AllowGenerationByRequest()
        {
            Strategy = ProxyGenerationStrategy.ByRequest;
            return this;
        }

        /// <summary>
        ///     Registers the <typeparamref name="T"/> proxy type required to proxy.
        /// </summary>
        public ProxyFactoryOptions Add<T>() => Add(typeof(T));

        /// <summary>
        ///     Registers the <paramref name="proxyType"/> required to proxy.
        /// </summary>
        public ProxyFactoryOptions Add(Type proxyType)
        {
            if (!proxyType.IsInterface)
                throw new ArgumentException($"Expected an interface type but provided `{proxyType.Name}` instead.", nameof(proxyType));

            proxyTypes.Add(proxyType);
            return this;
        }

        /// <summary>
        ///     Clears all configured proxy types.
        /// </summary>
        public ProxyFactoryOptions ClearAll()
        {
            proxyTypes.Clear();
            return this;
        }

        /// <summary>
        ///     Registered proxy types.
        /// </summary>
        internal Type[] ProxyTypes => proxyTypes.ToArray();
    }
}