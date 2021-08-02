using System;
using System.Linq.Expressions;
using System.Reflection;
using Assistant.Net.Dynamics.Abstractions;

namespace Assistant.Net.Dynamics.Abstractions
{
    public static class ProxyExtensions
    {
        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> to return <paramref name="result"/>.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            TResult result) =>
            proxy.Intercept(selector, (_, _, _) => result);

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/>.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<object?[], TResult> interceptor) =>
            proxy.Intercept(selector, (_, _, args) => interceptor(args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<Func<object?[], TResult>, object?[], TResult> interceptor) =>
            proxy.Intercept(selector, (next, _, args) => interceptor(next, args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T, TResult>(
            this Proxy<T> proxy,
            Expression<Func<T, TResult>> selector,
            Func<Func<object?[], TResult>, MethodInfo, object?[], TResult> interceptor)
        {
            switch (selector.Body)
            {
                case MemberExpression { Member: PropertyInfo { GetMethod: var getProperty/*, SetMethod: var setProperty*/ } }:
                    if (getProperty != null)
                        proxy.AddOrUpdate(getProperty!, (next, mi, args) => interceptor(x => (TResult) next(x)!, mi, args));
                    // note: setters are ignored as they weren't properly planned
                    // todo: implement setters
                    //if (setProperty != null)
                    //    proxy.AddOrUpdate(setProperty!, (next, method, args) => interceptor(x => (TResult) next(x)!, method, args));
                    return proxy;

                case MethodCallExpression { Method: var method }:
                    proxy.AddOrUpdate(method!, (next, mi, args) => interceptor(x => (TResult)next(x)!, mi, args));
                    return proxy;

                default:
                    throw new ArgumentException("Invalid expression value.", nameof(selector));
            }
        }

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/>.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<object?[]> interceptor) =>
            proxy.Intercept(selector, (_, _, args) => interceptor(args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<Action<object?[]>, object?[]> interceptor) =>
            proxy.Intercept(selector, (next, _, args) => interceptor(next, args));

        /// <summary>
        ///     Intercepts a method or property call defined in <paramref name="selector"/> with <paramref name="interceptor"/> in pipeline manner.
        /// </summary>
        public static Proxy<T> Intercept<T>(
            this Proxy<T> proxy,
            Expression<Action<T>> selector,
            Action<Action<object?[]>, MethodInfo, object?[]> interceptor)
        {
            switch (selector.Body)
            {
                case MemberExpression { Member: PropertyInfo { GetMethod: var getProperty } }:
                    proxy.AddOrUpdate(getProperty!, (next, method, args) =>
                    {
                        interceptor(x => next(x), method, args);
                        return (object?)null;
                    });
                    return proxy;

                case MethodCallExpression { Method: var getMethod }:
                    proxy.AddOrUpdate(getMethod!, (next, method, args) =>
                    {
                        interceptor(x => next(x), method, args);
                        return (object?)null;
                    });
                    return proxy;

                default:
                    throw new ArgumentException("Invalid expression value.", nameof(selector));
            }
        }

        private static void AddOrUpdate<TResult>(
            this IProxy proxy,
            MethodInfo method,
            Func<Func<object?[], object?>, MethodInfo, object?[], TResult> interceptor)
        {
            //if (!proxy.Interceptors.TryGetValue(method, out var interceptors))
            if (!proxy.Interceptors.ContainsKey(method))
                proxy.Interceptors.Add(method, (next, args) => interceptor(next, method!, args));
            else
                //interceptors += (next, args) => interceptor(next, method!, args);
                //proxy.Interceptors[method] = interceptors;
                proxy.Interceptors[method] += (next, args) => interceptor(next, method!, args);
        }
    }

}