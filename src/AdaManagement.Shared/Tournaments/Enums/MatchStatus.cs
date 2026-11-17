using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>Lifecycle state of a single match.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MatchStatus>))]
public enum MatchStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Forfeit = 3
}
