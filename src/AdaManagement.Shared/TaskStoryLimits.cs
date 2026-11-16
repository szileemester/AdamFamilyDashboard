namespace AdaManagement.Shared;

/// <summary>A teendők és bejegyzések korlátai: a szerver ezzel ellenőriz (és ez az oszlophossz), a kliens ezzel korlátoz.</summary>
public static class TaskStoryLimits
{
    public const int ChecklistTextMax = 200;
    public const int ChecklistMaxItems = 50;
    public const int StoryTextMax = 2000;
}
