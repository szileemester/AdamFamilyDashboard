using System.Text.Json.Serialization;

namespace AdaManagement.Shared.Tournaments;

/// <summary>How a match's detailed score entries are recorded and labeled.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreType>))]
public enum ScoreType
{
    Games = 0,
    Points = 1
}
