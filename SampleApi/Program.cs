using FileStorage;
using FormEventHandler;
using SampleApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7000);
});

builder.Services.AddControllers();

// Development: allow any localhost origin with credentials.
// Production: restrict to the specific origins your frontend is served from.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .WithExposedHeaders("Content-Disposition");
        }
        else
        {
            policy.WithOrigins("https://your-production-domain.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IFormLocalizer, FormLocalizer>();

builder.Services.AddSingleton<IAwsDemoService, AwsDemoService>();
builder.Services.AddSingleton<IFileStorageProvider, AwsS3StorageProvider>();
builder.Services.AddDynamoDb(builder.Configuration);
builder.Services.AddS3(builder.Configuration);
builder.Services.AddHostedService<SeedService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
