using Bookings.Payments;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Eventuous;
using Eventuous.AspNetCore;
using Eventuous.Postgresql;
using Serilog;

TypeMap.RegisterKnownEventTypes();
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddOpenTelemetry();
builder.Services.AddEventuous(builder.Configuration);

var app = builder.Build();

app.AddEventuousLogs();
app.UseSwagger().UseSwaggerUI();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Here we discover commands by their annotations
// app.MapDiscoveredCommands();
app.MapDiscoveredCommands<Payment>();



if (app.Configuration.GetValue<bool>("Postgres:InitializeDatabase")) {
    await InitialiseSchema(app);
}

app.Run();

async Task InitialiseSchema(IHost webApplication) {
    var options           = webApplication.Services.GetRequiredService<PostgresStoreOptions>();
    var schema            = new Schema(options.Schema);
    var connectionFactory = webApplication.Services.GetRequiredService<GetPostgresConnection>();
    await schema.CreateSchema(connectionFactory);
}
