using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>The two fixed sides of the house Unmatched ladder.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnmatchedTeam>))]
public enum UnmatchedTeam
{
    /// <summary>"Fiúk" - the blue side.</summary>
    Fiuk = 0,

    /// <summary>"Lányok" - the red side.</summary>
    Lanyok = 1
}
