using AdaManagement.Server.Tournaments.Domain.Entities;
using AdaManagement.Shared.Tournaments;

namespace AdaManagement.Server.Tournaments.Application;

/// <summary>Persistence boundary for the single <see cref="UnmatchedScoreboard"/> row.</summary>
public interface IUnmatchedRepository
{
    Task<UnmatchedScoreboard?> GetAsync(CancellationToken cancellationToken = default);

    Task AddAsync(UnmatchedScoreboard scoreboard, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
