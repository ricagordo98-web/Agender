using System.Text.Json.Serialization;

namespace Agender.Shared.Components;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Shortcut
{
    Palette
}
