using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Resource;
using Pege.Services;
using Pege.Startup;
using Pege.Streaming;
using Serilog;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Stream} {Message:lj}{NewLine}{Exception}"
        )
        .CreateLogger();

    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(options =>
    {
        // Разрешаем Kestrel пропускать Latin1/UTF-8 байты в заголовках ответа
        options.ResponseHeaderEncodingSelector = _ => System.Text.Encoding.GetEncoding("ISO-8859-1");
        // Или, если плееры современные:
        // options.ResponseHeaderEncodingSelector = _ => System.Text.Encoding.UTF8;
    });

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 2147483648;
    });

    builder.Host.UseSerilog();

    builder.Services.AddDbContextFactory<DataContext>(options =>
    options.UseSqlite(builder.Configuration["DataConnectionString"]));

    builder.Services.AddSingleton<FileLockManager>();

    builder.Services.AddSingleton<StreamFactory>();

    builder.Services.AddTransient<FFmpegService>();

    builder.Services.AddTransient<AudioStreamConnector>();

    builder.Services.AddSingleton(sp => new TelegramService(builder.Configuration["Telegram:BotToken"]));

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { typeInfo =>
            {
                // Находим базовый класс в конфигурации
                if (typeInfo.Type == typeof(StreamStatus))
                {
                    // Включаем полиморфизм программно
                    typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                    {
                        // Оставляем пустым, чтобы поле "$type" НЕ появлялось
                        TypeDiscriminatorPropertyName = null,
                        UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor
                    };

                    // Явно регистрируем всех наследников
                    typeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(typeof(AudioStreamStatus))
                    );
                    typeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(typeof(RelayAudioStreamStatus))
                    );
                    typeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(typeof(FileAudioStreamStatus))
                    );
                }
            }}
            };
        });

    var app = builder.Build();

    app.UseExceptionHandler();

    app.Services.GetService<TelegramService>()?.SetWebhookAsync(builder.Configuration["Telegram:Webhook"]!);

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Error(string.Format(Error.AppLaunchError, ex.Message));
}