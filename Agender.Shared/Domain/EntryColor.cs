using System.Text.Json.Serialization;

namespace Agender.Shared.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntryColor
{
    Indigo,
    Violet,
    Pink,
    Rose,
    Amber,
    Emerald,
    Teal,
    Sky
}
