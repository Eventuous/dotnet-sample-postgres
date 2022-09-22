using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Bookings.Payments.Integration;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Producers;
using Eventuous.Projections.MongoDB;
using Eventuous.RabbitMq.Producers;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace Bookings.Payments;

public static class Registrations {
    public static void AddServices(this IServiceCollection services, IConfiguration configuration) {
        NpgsqlConnection GetConnection() => new(configuration["Postgres:ConnectionString"]);

        var connectionFactory = new ConnectionFactory {
            Uri                    = new Uri(configuration["RabbitMq:ConnectionString"]),
            DispatchConsumersAsync = true
        };

        services.AddSingleton(connectionFactory);
        services.AddSingleton((GetPostgresConnection)GetConnection);
        services.AddAggregateStore<PostgresStore>();
        services.AddApplicationService<CommandService, Payment>();
        services.AddSingleton(Mongo.ConfigureMongo(configuration));
        services.AddCheckpointStore<MongoCheckpointStore>();
        services.AddEventProducer<RabbitMqProducer>();

        services
            .AddGateway<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, RabbitMqProducer>(
                "IntegrationSubscription",
                PaymentsGateway.Transform
            );
    }

    public static void AddOpenTelemetry(this IServiceCollection services) {
        services.AddOpenTelemetryMetrics(
            builder => builder
                .AddAspNetCoreInstrumentation()
                .AddEventuous()
                .AddEventuousSubscriptions()
                .AddPrometheusExporter()
        );

        services.AddOpenTelemetryTracing(
            builder => builder
                .AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddEventuousTracing()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("payments"))
                .SetSampler(new AlwaysOnSampler())
                .AddZipkinExporter()
        );
    }
}
