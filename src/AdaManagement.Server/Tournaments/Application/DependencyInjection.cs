using System.Globalization;
using AdaManagement.Server.Tournaments.Data;
using FluentValidation;

namespace AdaManagement.Server.Tournaments.Application;

/// <summary>A versenymodul (az Adaminatorból átvéve) szolgáltatásai: validátorok, use-case szolgáltatások, repositoryk.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTournaments(this IServiceCollection services)
    {
        // A saját üzenet nélküli szabályok (pl. érvénytelen enum) is magyarul szóljanak.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("hu");
        services.AddValidatorsFromAssemblyContaining<CreateTournamentRequestValidator>();
        services.AddScoped<TournamentService>();
        services.AddScoped<ParticipantService>();
        services.AddScoped<BracketService>();
        services.AddScoped<MatchService>();
        services.AddScoped<UnmatchedService>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<IUnmatchedRepository, UnmatchedRepository>();
        return services;
    }
}
