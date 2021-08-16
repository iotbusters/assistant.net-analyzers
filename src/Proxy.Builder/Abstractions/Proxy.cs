using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Assistant.Net.Dynamics.Abstractions
{
    /// <summary>
    ///     Proxy builder abstraction.
    /// </summary>
    /// <typeparam name="T">Proxy interface type.</typeparam>
    public class Proxy<T> : IProxy
    {
        Dictionary<MethodInfo, Func<Func<object?[], object?>, object?[], object?>> IProxy.Interceptors { get; } = new();

        /// <summary>
        ///     Resolves configured interception logic for the proxied method.
        /// </summary>
        protected Func<object?[], object?> GetImplementation(MethodInfo key, Func<object?[], object?> defaultBehaviour)
        {
            var interceptors = ((IProxy) this).Interceptors;
            if (!interceptors.TryGetValue(key, out var interceptor))
                return defaultBehaviour;

            return interceptor.GetInvocationList().Cast<Func<Func<object?[], object?>, object?[], object?>>().Aggregate(
                defaultBehaviour,
                (next, current) => args => current(next, args));
        }

        /// <summary>
        ///     Proxy object.
        /// </summary>
        public T Object => (T) (object) this;
    }
}