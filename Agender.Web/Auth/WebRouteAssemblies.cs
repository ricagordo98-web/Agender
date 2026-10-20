using System.Reflection;
using Agender.Shared.Abstractions;
using Agender.Web.Components;

namespace Agender.Web.Auth;

public class WebRouteAssemblies : IAdditionalRouteAssemblies
{
    public IReadOnlyList<Assembly> Assemblies { get; } = [typeof(App).Assembly];
}
