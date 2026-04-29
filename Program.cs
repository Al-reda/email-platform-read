using System.Text.Json.Serialization;
using EmailPlatform.Read.Endpoints;
using EmailPlatform.Shared.Clients;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, sp, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(new CompactJsonFormatter())
        .Enrich.FromLogContext());

    builder.Services.Configure<StorageClientOptions>(
        builder.Configuration.GetSection(StorageClientOptions.SectionName));

    builder.Services.AddHttpClient<IStorageClient, StorageClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<StorageClientOptions>>().Value;
        client.BaseAddress = new Uri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    }).AddStandardResilienceHandler();

    builder.Services.ConfigureHttpJsonOptions(o =>
    {
        o.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

    builder.Services.AddHealthChecks();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Read Service v1");
        o.RoutePrefix = "swagger";
    });

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "read" }));
    app.MapHealthChecks("/health/ready");

    app.MapReadEndpoints();

    var lifetime = app.Lifetime;
    lifetime.ApplicationStopping.Register(() =>
        Log.Information("Read service received shutdown signal — draining connections..."));
    lifetime.ApplicationStopped.Register(() =>
        Log.Information("Read service stopped cleanly."));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Read service failed to start");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
