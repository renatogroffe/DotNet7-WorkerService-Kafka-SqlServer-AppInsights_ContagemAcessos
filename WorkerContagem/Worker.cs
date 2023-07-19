using System.Diagnostics;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Confluent.Kafka;
using WorkerContagem.Data;
using WorkerContagem.Kafka;
using WorkerContagem.Models;

namespace WorkerContagem;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContagemRepository _repository;
    private readonly TelemetryConfiguration _telemetryConfig;
    private readonly string _topicName;
    private readonly string _groupId;
    private readonly IConsumer<Ignore, string> _consumer;

    public Worker(ILogger<Worker> logger,
        IConfiguration configuration,
        ContagemRepository repository,
        TelemetryConfiguration telemetryConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
        _telemetryConfig = telemetryConfig;

        _topicName = _configuration["ApacheKafka:Topic"]!;
        _groupId = _configuration["ApacheKafka:GroupId"]!;
        _consumer = KafkaExtensions.CreateConsumer(_configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Topic = {_topicName}");
        _logger.LogInformation($"Group Id = {_groupId}");
        _logger.LogInformation("Aguardando mensagens...");
        _consumer.Subscribe(_topicName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Run(() =>
            {
                ProcessResult(_consumer.Consume(stoppingToken));
            });
        }
    }

    private void ProcessResult(ConsumeResult<Ignore, string> result)
    {
        var start = DateTime.Now;
        var watch = new Stopwatch();
        watch.Start();

        var messageContent = result.Message.Value;
        _logger.LogInformation(
            $"[{_topicName} | Nova mensagem] " + messageContent);

        watch.Stop();
        TelemetryClient client = new(_telemetryConfig);
        client.TrackDependency(
            "Kafka", $"Consume {_topicName}",
            messageContent, start, watch.Elapsed, true);

        ResultadoContador? resultadoContador;
        try
        {
            resultadoContador = JsonSerializer.Deserialize<ResultadoContador>(messageContent,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            _logger.LogError("Dados invalidos para o Resultado!");
            resultadoContador = null;
        }
        if (resultadoContador is not null)
        {
            try
            {
                _repository.Save(resultadoContador);
                _logger.LogInformation("Resultado registrado com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro durante a gravacao: {ex.Message}");
            }
        }
    }
}