# Assistant.NET Dynamics

This is a subset of the [Assistant.NET](https://github.com/iotbusters/assistant.net/blob/master/README.md) solution.
The solution is planned to help with dynamic code generation like proxy or mapping.

Currently, it's in design and implementation stage, so the repository contains mostly tools and infrastructure parts only.
Existing releases cannot be assumed as stable and backward compatible too, so pay attention during package upgrade!

Hopefully, it will be useful for someone once main functional is ready.

Please join this [quick survey](https://forms.gle/eB3sN5Mw76WMpT6w5).

## Releases

- [Assistant.Net.Diagnostics Release 0.0.1](https://github.com/iotbusters/assistant.net.diagnostics/releases/tag/0.0.1)
  - Initial version

## Packages

A family of standalone packages serve Assistant.NET Dynamics needs and being [freely](license) distributed
at [nuget.org](https://nuget.org). Each of them has own responsibility and solves some specific aspect of the solution.

### assistant.net.dynamics

It's reserved for code usage analysis, runtime and compile-forward optimizations. E.g. Proxies, mappings etc.

#### assistant.net.dynamics.proxy.abstractions

Abstractions over proxy generation mechanism.

#### assistant.net.dynamics.proxy.analyzer

Analysis based proxy generation tool that supports compile-forward proxy generation according to the usage `factory.Create<Interface>()`.

#### assistant.net.dynamics.proxy.builder

Proxy source code generation tool.
