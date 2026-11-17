using AdaManagement.Server.Data;
using AdaManagement.Server.Tournaments.Application;
using AdaManagement.Server.Tournaments.Domain.Entities;
using AdaManagement.Shared.Tournaments;
using Microsoft.EntityFrameworkCore;

namespace AdaManagement.Server.Tournaments.Data;

public class UnmatchedRepository : IUnmatchedRepository
{
    private readonly AppDbContext _dbContext;

    public UnmatchedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Addressed by the fixed id rather than "whatever row is first": writes always target that id, so
    // an unfiltered read would let a stray second row show one board while the editor saved to another.
    // Picks are an owned collection, so EF loads them with their owner - the row comes back whole.
    public Task<UnmatchedScoreboard?> GetAsync(CancellationToken cancellationToken = default) =>
        _dbContext.UnmatchedScoreboard
            .FirstOrDefaultAsync(s => s.Id == UnmatchedScoreboard.SingletonId, cancellationToken);

    public async Task AddAsync(UnmatchedScoreboard scoreboard, CancellationToken cancellationToken = default) =>
        await _dbContext.UnmatchedScoreboard.AddAsync(scoreboard, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
