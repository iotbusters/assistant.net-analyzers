using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Proxy.Runtime.Tests.Mocks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Assistant.Net.Dynamics.Proxy.Runtime.Tests
{
    public class Tests
    {
        [Test, Ignore("Manual tests only")]
        public void Proxy_isNotSlowerThen100Times()
        {
            var @object = new Test();

            var compilation = CSharpCompilation.Create("assembly-name")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddProxy<ITest>();

            using var memory = new MemoryStream();
            var result = compilation.Emit(memory);

            result.Success.Should().BeTrue($"Compilation failed with {result.Diagnostics.Length} messages");

            memory.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(memory.ToArray());
            var type = assembly.GetType(typeof(ITest).FullName + "Proxy")!;
            var proxy = ((Proxy<ITest>)Activator.CreateInstance(type, @object)!)
                .Intercept(x => x.Method<string>(default!), (_, _, _) => { })
                .Intercept(x => x.Method(), (_, _, _) => { })
                .Intercept(x => x.Property, "5")
                .Intercept(x => x.Function(), _ => "6")
                .Intercept(x => x.Function(default!), (_, _) => "7")
                .Intercept(x => x.Function(default!, default!), (_, _, _) => 8)
                .Object;

            var watch = Stopwatch.StartNew();

            for (var i = 0; i < 1_000_000; i++)
            {
                @object.Method("1");
                var a = @object.Method();
                var b = @object.Property;
                var c = @object.Function();
                var d = @object.Function("1");
                var e = @object.Function("1", 2);
            }

            var iCallTime = watch.Elapsed;

            for (int i = 0; i < 1_000_000; i++)
            {
                proxy.Method("1");
                var a = proxy.Method();
                var b = proxy.Property;
                var c = proxy.Function();
                var d = proxy.Function("1");
                var e = proxy.Function("1", 2);
            }

            watch.Stop();

            var pCallTime = watch.Elapsed - iCallTime;
            var diff = pCallTime / iCallTime;

            diff.Should().BeLessOrEqualTo(100, "Proxy isn't slower then 100 times");
        }

        [Test]
        public void Proxy_interceptsGetOperations()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var _ = KnownProxy.ProxyTypes;

            var proxy = factory.Create<ITest>()
                .Intercept(x => x.Method(), (_, _, _) => Task.CompletedTask)
                .Intercept(x => x.Method(""), (_, _, _) => "3")
                .Intercept(x => x.Property, "5")
                .Intercept(x => x.Function(), _ => "6")
                .Intercept(x => x.Function(default!), (_, _) => "7")
                .Intercept(x => x.Function(default!, default!), (_, _, _) => 8)
                .Object;

            proxy.ToString().Should().Be(proxy.GetType().FullName);
            proxy.Method().Should().Be(Task.CompletedTask);
            proxy.Method("1").Should().Be("3");
            proxy.Property.Should().Be("5");
            proxy.Function().Should().Be("6");
            proxy.Function(default!).Should().Be("7");
            proxy.Function(default!, default).Should().Be(8);
        }

        [Test]
        public void Proxy_throws_noBackedObjectAndNoInterceptors()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var proxy = factory.Create<ITest>().Object;

            proxy.Invoking(x => x.Method()).Should().Throw<InvalidOperationException>();
        }
    }
}