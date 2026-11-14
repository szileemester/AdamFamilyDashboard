namespace AdaManagement.Shared;

public record TaskDto(
    int Id,
    string Title,
    string? Notes,
    bool IsImportant,
    bool IsUrgent,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int? RecurringTemplateId,
    DateOnly? OccurrenceDate,
    string? Icon);

/// <param name="Icon">Kézzel választott ikon kulcsa (null = automatikus a cím alapján).</param>
public record TaskEditDto(string Title, string? Notes, bool IsImportant, bool IsUrgent, DateOnly? DueDate, string? Icon);

public record TemplateDto(
    int Id,
    string Title,
    string? Notes,
    int IntervalMonths,
    DateOnly NextDueDate,
    DateOnly SuggestedFollowingDueDate,
    int LeadDays,
    bool IsActive);

public record TemplateEditDto(string Title, string? Notes, int IntervalMonths, DateOnly NextDueDate, int LeadDays, bool IsActive);

/// <summary>Egy ismétlődő alkalom teljesítése. A NextDueDate a felajánlott vagy kézzel átírt következő esedékesség.</summary>
public record CompleteOccurrenceDto(DateOnly CompletedOn, DateOnly NextDueDate, string? Note);

public record CompletionLogDto(int Id, DateOnly DueDate, DateOnly CompletedOn, string? Note);

public record BirthdayDto(int Id, string Name, int Month, int Day, int? Year, string? Notes);

public record BirthdayEditDto(string Name, int Month, int Day, int? Year, string? Notes);

public record UpcomingBirthdayDto(int Id, string Name, DateOnly Date, int DaysUntil, int? TurningAge);

public record UpcomingMaintenanceDto(int TemplateId, string Title, DateOnly DueDate, int DaysUntil);

public record TradespersonDto(int Id, string Occupation, string Name, string? Phone, string? Email, string? Website, string? Notes);

public record TradespersonEditDto(string Occupation, string Name, string? Phone, string? Email, string? Website, string? Notes);

public record SettingsDto(string WeatherLabel, double Latitude, double Longitude, int BirthdayLookaheadDays);

public record DashboardDto(
    DateOnly Today,
    IReadOnlyList<TaskDto> OpenTasks,
    IReadOnlyList<UpcomingBirthdayDto> Birthdays,
    IReadOnlyList<UpcomingMaintenanceDto> Maintenance,
    SettingsDto Settings);

public record MeDto(string Email, string? Name);
