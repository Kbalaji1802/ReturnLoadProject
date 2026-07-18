using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Loads;
using ReturnLoad.Application.UseCases.Onboarding;
using ReturnLoad.Application.UseCases.Trips;

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

        // Use-case application services (M4 sprint). Implementations are internal to this layer.
        services.AddScoped<ICarrierService, CarrierService>();
        services.AddScoped<IDriverOnboardingService, DriverOnboardingService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ILoadService, LoadService>();
        services.AddScoped<ITripService, TripService>();

        return services;
    }
}
