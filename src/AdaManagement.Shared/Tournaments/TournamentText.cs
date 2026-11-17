namespace AdaManagement.Shared.Tournaments;

/// <summary>A versenymodul magyar feliratai és a felület apró számításai.</summary>
public static class TournamentText
{
    /// <summary>Emoji helyett ez látszik, amíg a résztvevőnek nincs saját.</summary>
    public const string DefaultEmoji = "😐";

    public static string Label(this TournamentType type) => type switch
    {
        TournamentType.SingleElimination => "Egyenes kiesés",
        TournamentType.DoubleElimination => "Dupla kiesés",
        TournamentType.RoundRobin => "Körmérkőzés",
        TournamentType.GroupStagePlayoff => "Csoportkör + rájátszás",
        _ => type.ToString(),
    };

    public static string Label(this TournamentStatus status) => status switch
    {
        TournamentStatus.Planned => "Tervezett",
        TournamentStatus.Running => "Fut",
        TournamentStatus.Finished => "Befejezett",
        _ => status.ToString(),
    };

    public static string Label(this MatchFormat format) => format switch
    {
        MatchFormat.Bo1 => "1 játszma",
        MatchFormat.Bo2 => "2 játszma (döntetlen lehet)",
        MatchFormat.Bo3 => "3-ból 2 nyert",
        MatchFormat.Bo5 => "5-ből 3 nyert",
        MatchFormat.Bo7 => "7-ből 4 nyert",
        _ => format.ToString(),
    };

    public static string Label(this ScoreType type) => type switch
    {
        ScoreType.Games => "Játszmák",
        ScoreType.Points => "Pontok",
        _ => type.ToString(),
    };

    public static string Label(this TiebreakerPolicy policy) => policy switch
    {
        TiebreakerPolicy.ComputedThenMatch => "Egymás elleni eredmény, utána döntőmeccs",
        TiebreakerPolicy.AlwaysMatch => "Mindig döntőmeccs",
        _ => policy.ToString(),
    };

    public static string Label(this GroupStageKind kind) => kind switch
    {
        GroupStageKind.RoundRobin => "Körmérkőzéses csoportok",
        GroupStageKind.Swiss => "Svájci rendszer (egy mezőny)",
        _ => kind.ToString(),
    };

    public static string Label(this PlayoffKind kind) => kind switch
    {
        PlayoffKind.DoubleElimination => "Dupla kiesés",
        PlayoffKind.SingleElimination => "Egyenes kiesés",
        _ => kind.ToString(),
    };

    /// <summary>A verseny állapotának CSS-osztálya (kártya, címke).</summary>
    public static string CssClass(this TournamentStatus status) => status switch
    {
        TournamentStatus.Running => "running",
        TournamentStatus.Planned => "planned",
        _ => "finished",
    };

    /// <summary>Eldőlt meccs: lezárva vagy feladással.</summary>
    public static bool IsDecided(this MatchStatus status) => status is MatchStatus.Completed or MatchStatus.Forfeit;

    /// <summary>A forduló minden meccse eldőlt.</summary>
    public static bool IsDecided(this BracketRoundDto round) => round.Matches.All(m => m.Status.IsDecided());

    /// <summary>„emoji név” szövegben (párbeszédablakok); emoji nélkül az alapértelmezett, mint a ParticipantLabel-nél.</summary>
    public static string DisplayName(string name, string? emoji) => $"{emoji ?? DefaultEmoji} {name}";

    /// <summary>0-tól számozott csoport neve: „A csoport”, „B csoport”…</summary>
    public static string GroupLabel(int groupIndex) => $"{(char)('A' + groupIndex)} csoport";

    /// <summary>
    /// Az ág mérete: a résztvevőszámnál nem kisebb legkisebb kettőhatvány (dupla kiesésnél legalább 4).
    /// Körmérkőzésnél és csoportkörnél nincs kitöltés, ott a létszám maga.
    /// </summary>
    public static int BracketSize(int participantCount, TournamentType type)
    {
        if (participantCount < 2) return 0;
        if (type is TournamentType.RoundRobin or TournamentType.GroupStagePlayoff) return participantCount;
        var size = 1;
        while (size < participantCount) size <<= 1;
        return type == TournamentType.DoubleElimination ? Math.Max(4, size) : size;
    }

    /// <summary>Ennyi erőnyerő kell az első fordulóban (körmérkőzésnél és csoportkörnél soha).</summary>
    public static int RequiredByes(int participantCount, TournamentType type) =>
        participantCount < 2 ? 0 : BracketSize(participantCount, type) - participantCount;
}

/// <summary>
/// Mely beállítások tartoznak egy versenyformához. Az űrlap és a részletoldal áttekintése is ezt használja,
/// így amit az űrlap elrejt, azt az áttekintés sem mutatja.
/// </summary>
public sealed record TournamentShape(
    bool IsGroupStagePlayoff,
    bool IsRoundRobin,
    bool UsesRoundRobinGroups,
    bool UsesSwissPool,
    bool PlayoffIsSingleElimination,
    bool ShowsSingleFormat,
    bool UsesBracketFormats,
    bool ShowsAllBracketFormats,
    bool ShowsTiebreakerPolicy,
    bool ShowsRules)
{
    public static TournamentShape For(TournamentType type, GroupStageKind groupStageKind, PlayoffKind playoffKind)
    {
        var groups = type == TournamentType.GroupStagePlayoff;
        var singleElimPlayoff = type == TournamentType.SingleElimination || (groups && playoffKind == PlayoffKind.SingleElimination);
        return new TournamentShape(
            IsGroupStagePlayoff: groups,
            IsRoundRobin: type == TournamentType.RoundRobin,
            UsesRoundRobinGroups: groups && groupStageKind == GroupStageKind.RoundRobin,
            UsesSwissPool: groups && groupStageKind == GroupStageKind.Swiss,
            PlayoffIsSingleElimination: singleElimPlayoff,
            ShowsSingleFormat: type is TournamentType.SingleElimination or TournamentType.RoundRobin,
            UsesBracketFormats: type == TournamentType.DoubleElimination || groups,
            ShowsAllBracketFormats: type == TournamentType.DoubleElimination || (groups && playoffKind == PlayoffKind.DoubleElimination),
            ShowsTiebreakerPolicy: type == TournamentType.RoundRobin || groups,
            ShowsRules: singleElimPlayoff || type == TournamentType.RoundRobin || groups);
    }
}
