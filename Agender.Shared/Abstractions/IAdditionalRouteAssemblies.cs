using System.Reflection;

namespace Agender.Shared.Abstractions;

public interface IAdditionalRouteAssemblies
{
    IReadOnlyList<Assembly> Assemblies { get; }
}
