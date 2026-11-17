namespace AdaManagement.Shared.Tournaments;

/// <summary>A versenymodul hosszkorlátai és létszámhatárai: a domain és a kliens ugyanezeket használja.</summary>
public static class TournamentLimits
{
    public const int NameMaxLength = 50;
    public const int NotesMaxLength = 2000;
    public const int ParticipantNameMaxLength = 30;
    public const int MinParticipants = 2;
    public const int MaxParticipants = 32;
    public const int MaxSwissRounds = MaxParticipants - 1;
    public const int MaxGroups = 16;
}
