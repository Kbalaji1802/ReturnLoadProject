using Asp.Versioning;
using Microsoft.OpenApi.Models;

namespace ReturnLoad.Api.Extensions;

/// <summary>
/// Configures URL-segment API versioning (03_TECHNICAL_BIBLE.md §6: routes under
/// <c>/api/v1/...</c>) and Swagger/OpenAPI documentation.
/// </summary>
public static class ApiDocumentationExtensions
{
    public static IServiceCollection AddApiVersioningConfigured(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    public static IServiceCollection AddSwaggerConfigured(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ReturnLoad API",
                Version = "v1",
                Description =
                    "Foundation API for the ReturnLoad logistics platform. Only the "
                    + "health endpoint is exposed at this stage — business modules follow.",
            });

            // Advertise bearer auth in the Swagger UI so secured endpoints (task
            // T-013) are testable later. This defines documentation only; it does
            // not create any authentication behaviour.
            OpenApiSecurityScheme bearerScheme = new()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a JWT access token to authorise requests.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [bearerScheme] = [],
            });
        });

        return services;
    }
}
