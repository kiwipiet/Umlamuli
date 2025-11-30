Umlamuli
=======

![CI](https://github.com/Umlamuli/Umlamuli/workflows/CI/badge.svg)
[![NuGet](https://img.shields.io/nuget/dt/umlamuli.svg)](https://www.nuget.org/packages/umlamuli)
[![NuGet](https://img.shields.io/nuget/vpre/umlamuli.svg)](https://www.nuget.org/packages/umlamuli)
[![MyGet (dev)](https://img.shields.io/myget/umlamuli-ci/v/Umlamuli.svg)](https://myget.org/gallery/umlamuli-ci)

# About Umlamuli
Umlamuli is an open-source continuation of the MediatR library.  MediatR has changed it licence to a commercial one starting with version 13.
I have complete respect for Jimmy Bogard and the work he has done on MediatR, but I want to keep using an open-source mediator library.  I also feel that the Mediator pattern is a useful one to have available in .NET projects, but as it is fairly simple to implement, I have decided to create this fork and maintain it going forward.

[Umlamuli](https://isizulu.net/?umlamuli) [umlaˈmuːli] is a Zulu word,  means "mediator" or "arbiter".
I'm not a native Zulu speaker, so if I've made any mistakes with the name, please let me know!

This should be a drop-in replacement for MediatR v12.5.0.  The namespaces and package names have been changed from MediatR to Umlamuli.

# Umlamuli
Simple, unambitious mediator implementation in .NET.

In-process messaging with no dependencies.

This repository has been renamed from Umlamuli to Umlamuli. All namespaces, assemblies, and project names now use `Umlamuli`.
Supports request/response, commands, queries, notifications and events, synchronous and async with intelligent dispatching via C# generic variance.

Examples in the [wiki](https://github.com/Umlamuli/Umlamuli/wiki).

### Installing Umlamuli

You should install [Umlamuli with NuGet](https://www.nuget.org/packages/Umlamuli):

    Install-Package Umlamuli

Or via the .NET Core command line interface:

    dotnet add package Umlamuli

Either commands, from Package Manager Console or .NET Core CLI, will download and install Umlamuli and all required dependencies.

### Using Contracts-Only Package

To reference only the contracts for Umlamuli, which includes:

- `IRequest` (including generic variants)
- `INotification`
- `IStreamRequest`

Add a package reference to [Umlamuli.Contracts](https://www.nuget.org/packages/Umlamuli.Contracts)

This package is useful in scenarios where your Umlamuli contracts are in a separate assembly/project from handlers. Example scenarios include:
- API contracts
- GRPC contracts
- Blazor

### Registering with `IServiceCollection`

Umlamuli supports `Microsoft.Extensions.DependencyInjection.Abstractions` directly. To register various Umlamuli services and handlers:

```
services.AddUmlamuli(cfg => cfg.RegisterServicesFromAssemblyContaining<Startup>());
```

or with an assembly:

```
services.AddUmlamuli(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
```

This registers:

- `IMediator` as transient
- `ISender` as transient
- `IPublisher` as transient
- `IRequestHandler<,>` concrete implementations as transient
- `IRequestHandler<>` concrete implementations as transient
- `INotificationHandler<>` concrete implementations as transient
- `IStreamRequestHandler<>` concrete implementations as transient
- `IRequestExceptionHandler<,,>` concrete implementations as transient
- `IRequestExceptionAction<,>)` concrete implementations as transient

This also registers open generic implementations for:

- `INotificationHandler<>`
- `IRequestExceptionHandler<,,>`
- `IRequestExceptionAction<,>`

To register behaviors, stream behaviors, pre/post processors:

```csharp
services.AddUmlamuli(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly);
    cfg.AddBehavior<PingPongBehavior>();
    cfg.AddStreamBehavior<PingPongStreamBehavior>();
    cfg.AddRequestPreProcessor<PingPreProcessor>();
    cfg.AddRequestPostProcessor<PingPongPostProcessor>();
    cfg.AddOpenBehavior(typeof(GenericBehavior<,>));
    });
```

With additional methods for open generics and overloads for explicit service types.
