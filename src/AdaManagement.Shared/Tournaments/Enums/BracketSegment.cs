using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>
/// Which part of the bracket a match belongs to. Single Elimination uses <see cref="Winner"/> and
/// (optionally) <see cref="ThirdPlace"/>. <see cref="Loser"/>/<see cref="GrandFinal"/> are Double
/// Elimination only. <see cref="RoundRobin"/> is a flat, unrouted list of matches - no advancement.
/// <see cref="Tiebreaker"/> matches are also flat and unrouted (a mini round-robin among tied
/// participants); they only reorder a tied cohort in the standings, never feed another match.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BracketSegment>))]
public enum BracketSegment
{
    Winner = 0,
    Loser = 1,
    GrandFinal = 2,
    ThirdPlace = 3,
    RoundRobin = 4,
    Tiebreaker = 5
}
