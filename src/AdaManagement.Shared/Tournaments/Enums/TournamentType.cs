using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>
/// The elimination structure used by a tournament.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TournamentType>))]
public enum TournamentType
{
    SingleElimination = 0,
    DoubleElimination = 1,
    RoundRobin = 2,

    /// <summary>
    /// Two-stage (TI-style): a group stage, then a manually-started playoff seeded from its standings.
    /// <see cref="GroupStageKind"/> chooses how the group stage is played (round-robin groups or one
    /// Swiss pool) and <see cref="PlayoffKind"/> chooses the playoff's structure.
    /// </summary>
    GroupStagePlayoff = 3
}
