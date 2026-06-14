using Amazon.S3;
using CerberusBusinessService.DataSecure;
using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Candidatos;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.JWT;
using CerberusBusinessService.Models.R2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// ===== JWT settings =====
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.Configure<R2Settings>(
    builder.Configuration.GetSection("R2")
);
builder.Services.Configure<WsOptions>(builder.Configuration.GetSection("WebServices:Abac"));
builder.Services.AddSingleton(new ConnectionStringProvider(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton(new ConnectionStringProvider(builder.Configuration.GetConnectionString("CerberusConfig")));
builder.Services.AddSingleton(jwtSettings);
// ===== R2 =====

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<R2Settings>>().Value;

    var config = new AmazonS3Config
    {
        ServiceURL = settings.ServiceUrl,
        ForcePathStyle = true
    };

    return new AmazonS3Client(
        settings.AccessKey,
        settings.SecretKey,
        config
    );
});

// ===== Auth JWT =====
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddScoped<ListadoEmpleadosFunctions>();
builder.Services.AddScoped<EditarEmpleadoFunctions>();
builder.Services.AddScoped<AltaDomiciliosFunctions>();
builder.Services.AddScoped<ListadoDomiciliosFunctions>();
builder.Services.AddScoped<EliminadoDomicilioFunctions>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<CandidatosFunctions>();
builder.Services.AddScoped<SaludFunctions>();
builder.Services.AddHttpClient<ValidaAccionFunction>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<WsOptions>>().Value;

    http.BaseAddress = new Uri(opt.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpClient<AltaEmpleadoFuncions>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<WsOptions>>().Value;

    http.BaseAddress = new Uri(opt.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UsePathBase("/Negocio");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(); // UI en /swagger
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
