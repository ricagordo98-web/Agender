using Agender.Shared.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Agender.Web.Auth;

public class WebShareLinkBuilder(NavigationManager navigation) : IShareLinkBuilder
{
    public string Build(string token) => navigation.ToAbsoluteUri($"/p/{token}").ToString();
}
