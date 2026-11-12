namespace AdaManagement.Server.Domain;

public static class OccurrenceGenerator
{
    /// <summary>
    /// Lusta generálás: minden aktív sablonhoz, amelynek esedékessége LeadDays napon belül van (vagy már elmúlt),
    /// létrehoz egy „Fontos, nem sürgős” feladatot, ha az adott alkalomhoz még nincs feladat. Idempotens.
    /// </summary>
    public static IReadOnlyList<TaskItem> TasksToCreate(
        DateOnly today,
        DateTime nowUtc,
        IEnumerable<RecurringTemplate> templates,
        IEnumerable<(int TemplateId, DateOnly OccurrenceDate)> existingOccurrences)
    {
        var existing = existingOccurrences.ToHashSet();
        var result = new List<TaskItem>();

        foreach (var t in templates)
        {
            if (!t.IsActive) continue;
            if (t.NextDueDate.AddDays(-t.LeadDays) > today) continue;
            if (existing.Contains((t.Id, t.NextDueDate))) continue;

            result.Add(new TaskItem
            {
                Title = t.Title,
                Notes = t.Notes,
                IsImportant = true,
                IsUrgent = false,
                DueDate = t.NextDueDate,
                CreatedAt = nowUtc,
                RecurringTemplateId = t.Id,
                OccurrenceDate = t.NextDueDate,
            });
        }

        return result;
    }
}
