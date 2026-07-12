using Xunit;

// These tests each boot the API in-memory via WebApplicationFactory. Because the
// host configures process-global state (notably Serilog's static Log.Logger, which
// Program.cs sets on start and flushes on shutdown), running test classes in
// parallel lets one host's startup/teardown race another's. Serialising the suite
// keeps it deterministic; the suite is small and fast, so the cost is negligible.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
