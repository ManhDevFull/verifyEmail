using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using Verify.Data;
using Verify.Email;
using Verify.Models;
using Verify.Services;
using Verify.Validation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<MailjetOptions>(builder.Configuration.GetSection("Mailjet"));
builder.Services.AddHttpClient<IEmailSender, MailjetEmailSender>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
var connectionString = builder.Configuration.GetConnectionString("VerifyDatabase")
    ?? throw new InvalidOperationException("Connection string 'VerifyDatabase' was not found.");
builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddHostedService<OtpCleanupService>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler();

app.MapControllers();

app.Run();
