using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ReturnLoad.Application;

/// <summary>
/// Application-layer composition root. Registers cross-cutting application services
/// by scanning this assembly for FluentValidation validators.
/// <para>
/// No business handlers or validators exist yet; this is the wiring the foundation
/// needs so future features register automatically without touching startup code
/// (Open/Closed Principle). Object-mapping registration is deferred with the mapper
/// dependency itself — see 06_DECISION_LOG.md ADR-0007.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly applicationAssembly = typeof(IApplicationMarker).Assembly;

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        return services;
    }
}
