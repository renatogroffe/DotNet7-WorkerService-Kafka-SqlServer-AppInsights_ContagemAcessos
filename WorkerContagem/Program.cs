using Microsoft.ApplicationInsights.DependencyCollector;
using WorkerContagem;
using WorkerContagem.Data;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<ContagemRepository>();
        services.AddHostedService<Worker>();

        services.AddApplicationInsightsTelemetryWorkerService(options =>
        {
            options.ConnectionString =
                hostContext.Configuration.GetConnectionString("ApplicationInsights");
        });

        services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
        {
            module.EnableSqlCommandTextInstrumentation = true;
        });
    }).Build();

host.Run();