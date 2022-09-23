using System.Text.Json;
using Bookings.Application;
using Bookings.Application.Queries;
using Bookings.Domain;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Bookings.Integration;
using Eventuous;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace Bookings;

public static class Registrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        DefaultEventSerializer.SetDefaultSerializer(
            new DefaultEventSerializer(
                new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
            )
        );

        NpgsqlConnection GetConnection() => new(configuration["Postgres:ConnectionString"]);
        
        var connectionFactory = new ConnectionFactory {
            Uri                    = new Uri(configuration["RabbitMq:ConnectionString"]),
            DispatchConsumersAsync = true
        };

        services.AddSingleton(connectionFactory);
        services.AddSingleton((GetPostgresConnection)GetConnection);
        services.AddSingleton(new PostgresStoreOptions());
        services.AddAggregateStore<PostgresStore>();
        services.AddApplicationService<BookingsCommandService, Booking>();

        services.AddSingleton<Services.IsRoomAvailable>((id, period) => new ValueTask<bool>(true));

        services.AddSingleton<Services.ConvertCurrency>((from, currency) => new Money(from.Amount * 2, currency));

        services.AddSingleton(Mongo.ConfigureMongo(configuration));
        services.AddCheckpointStore<MongoCheckpointStore>();

        services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
            "BookingsProjections",
            builder => builder
                .AddEventHandler<BookingStateProjection>()
                .AddEventHandler<MyBookingsProjection>()
                .WithPartitioningByStream(2)
        );
        
        services.AddSubscription<RabbitMqSubscription, RabbitMqSubscriptionOptions>(
            "PaymentIntegration",
            builder => builder
                .Configure(x => x.Exchange = PaymentsIntegrationHandler.Stream)
                .AddEventHandler<PaymentsIntegrationHandler>()
        );
    }

    public static void AddOpenTelemetry(this IServiceCollection services) {
        var otelEnabled = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") != null;
        services.AddOpenTelemetryMetrics(
            builder => {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("bookings"))
                    .AddAspNetCoreInstrumentation()
                    .AddEventuous()
                    .AddEventuousSubscriptions()
                    .AddPrometheusExporter();
                if (otelEnabled) builder.AddOtlpExporter();
            }
        );

        services.AddOpenTelemetryTracing(
            builder => {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("bookings"))
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation()
                    .AddNpgsql()
                    .AddEventuousTracing()
                    .AddMongoDBInstrumentation();

                if (otelEnabled)
                    builder.AddOtlpExporter();
                else
                    builder.AddZipkinExporter();
            }
        );
    }
}