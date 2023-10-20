using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking;

public class MainIntegrated
{
    public static readonly CancellationTokenSource MasterCancellationTokenSource = new();
    private readonly ILogger _logger;
    private readonly ILibManager _libManager;
    private readonly UnifiedTrackingMutator _mutator;

    public MainIntegrated(ILoggerFactory loggerFactory, ILibManager libManager, UnifiedTrackingMutator mutator)
    {
        _logger = loggerFactory.CreateLogger("MainStandalone");
        _libManager = libManager;
        _mutator = mutator;
    }

    public void Teardown()
    {
        _logger.LogInformation("VRCFT Standalone Exiting!");
        _libManager.TeardownAllAndResetAsync();

        _mutator.SaveCalibration();

        // Kill our threads
        _logger.LogDebug("Cancelling token sources...");
        MasterCancellationTokenSource.Cancel();
        
        _logger.LogDebug("Resetting our time end period...");
        Utils.TimeEndPeriod(1);
        
        _logger.LogDebug("Teardown successful. Awaiting exit...");
    }

    public async Task InitializeAsync()
    {
        _libManager.Initialize();
        _mutator.LoadCalibration();

        // Begin main update loop
        _logger.LogDebug("Starting update loop...");
        Utils.TimeBeginPeriod(1);
        ThreadPool.QueueUserWorkItem(ct =>
        {
            var token = (CancellationToken)ct;
            
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(10);
                UnifiedTracking.UpdateData();
            }
        }, MasterCancellationTokenSource.Token);

        await Task.CompletedTask;
    }
}