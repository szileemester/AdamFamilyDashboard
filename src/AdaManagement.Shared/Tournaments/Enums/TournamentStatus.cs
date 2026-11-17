using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>
/// Lifecycle state of a tournament: Planned -&gt; Running -&gt; Finished.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TournamentStatus>))]
public enum TournamentStatus
{
    Planned = 0,
    Running = 1,
    Finished = 2
}
