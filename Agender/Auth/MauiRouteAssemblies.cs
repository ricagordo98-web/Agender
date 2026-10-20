using System.Reflection;
using Agender.Shared.Abstractions;

namespace Agender.Auth;

public sealed class MauiRouteAssemblies : IAdditionalRouteAssemblies
{
    public IReadOnlyList<Assembly> Assemblies { get; } = [typeof(MauiRouteAssemblies).Assembly];
}
