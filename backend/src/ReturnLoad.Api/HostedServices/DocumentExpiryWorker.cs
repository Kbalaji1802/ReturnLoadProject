using Microsoft.Extensions.Options;
using ReturnLoad.Application.UseCases.Documents;

namespace ReturnLoad.Api.HostedServices;

/// <summary>
/// Runs the document-expiry sweep on a timer (RC-2 Parts 13 &amp; 17).
/// <para>
/// The worker owns only scheduling; the decision of what is due belongs to
/// <see cref="IDocumentExpiryService"/> in the Application layer, which keeps the rule testable
/// without a host. A scope is created per pass because the service and its repositories are
/// scoped, and a singleton hosted service must not capture a scoped DbContext.
/// </para>
/// </summary>
internal sealed class DocumentExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<DocumentExpiryOptions> _options;
    private readonly ILogger<DocumentExpiryWorker> _logger;

    public DocumentExpiryWorker(
        IServiceScopeFactory scopes,
        IOptionsMonitor<DocumentExpiryOptions> options,
        ILogger<DocumentExpiryWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Document expiry sweep is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                IDocumentExpiryService expiry = scope.ServiceProvider.GetRequiredService<IDocumentExpiryService>();

                int sent = await expiry.SweepAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("Document expiry sweep sent {Count} reminder(s).", sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed pass must not kill the worker — the next tick retries. Logged rather
                // than swallowed (01_PROJECT_RULES.md §1.5) so a persistent failure is visible.
                _logger.LogError(ex, "Document expiry sweep failed; retrying at the next interval.");
            }

            try
            {
                await Task.Delay(_options.CurrentValue.SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
