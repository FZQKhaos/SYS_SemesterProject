using RabbitMQ.Client;
using Rebus.Bus;

namespace ProfileMicroservice.Messaging;

// Fortæller resten af programmet, om Rebus-bussen er startet og klar.
// Singleton: der findes kun én, og alle ser den samme værdi.
public sealed class RebusReadiness
{
    // volatile: en tråd (baggrundstjenesten) skriver, andre tråde (requests) læser.
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;
}

// record: en kort måde at lave en uforanderlig "pakke" af data.
public sealed record RebusBrokerSettings(string ConnectionString);

// BackgroundService: kører i baggrunden, mens resten af programmet starter og kører.
public sealed class RebusStartupService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceProvider _services;
    private readonly RebusBrokerSettings _settings;
    private readonly RebusReadiness _readiness;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<RebusStartupService> _logger;

    public RebusStartupService(
        IServiceProvider services,
        RebusBrokerSettings settings,
        RebusReadiness readiness,
        IHostApplicationLifetime lifetime,
        ILogger<RebusStartupService> logger)
    {
        _services = services;
        _settings = settings;
        _readiness = readiness;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Forlad opstartstråden med det samme, så værten kan blive færdig med at starte,
        // uanset hvor længe RabbitMQ om at svare.
        await Task.Yield();

        var delay = InitialDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitForBrokerAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RabbitMQ is not ready for the Rebus saga bus. Retrying in {Delay}.",
                    delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
                continue;
            }

            StartBus();
            return;
        }
    }

    // Kan vi logge ind på RabbitMQ med de samme oplysninger som Rebus bruger?
    // Lykkes det, lukkes forbindelsen igen med det samme (await using).
    private async Task WaitForBrokerAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_settings.ConnectionString),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
    }

    private void StartBus()
    {
        try
        {
            // Første gang IBus bliver bedt om, opretter Rebus bussen:
            // den deklarerer køen, abonnerer på events og begynder at modtage beskeder.
            _ = _services.GetRequiredService<IBus>();

            _readiness.MarkReady();
            _logger.LogInformation("Rebus saga bus started.");
        }
        catch (Exception ex)
        {
            // Rebus husker en mislykket oprettelse, så et nyt forsøg i samme proces giver samme fejl.
            // Vi lukker derfor ned med fejlkode 1, så Docker (restart: on-failure) starter servicen igen.
            _logger.LogCritical(
                ex,
                "Rebus saga bus could not be created although RabbitMQ was reachable. Stopping the application.");

            Environment.ExitCode = 1;
            _lifetime.StopApplication();
        }
    }
}