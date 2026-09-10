using Amazon.S3;
using CerberusBusinessService.DataSecure;
using CerberusBusinessService.Functions;
using CerberusBusinessService.Functions.Asistencias;
using CerberusBusinessService.Functions.Candidatos;
using CerberusBusinessService.Functions.Contratacion;
using CerberusBusinessService.Functions.Notificaciones;
using CerberusBusinessService.Functions.R2;
using CerberusBusinessService.Functions.Relevos;
using CerberusBusinessService.Functions.Supervision;
using CerberusBusinessService.Models.DTO;
using CerberusBusinessService.Models.JWT;
using CerberusBusinessService.Models.Notificaciones;
using CerberusBusinessService.Models.R2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

#region CONFIGURACION

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);

builder.Services.Configure<R2Settings>(builder.Configuration.GetSection("R2"));
builder.Services.Configure<WsOptions>(builder.Configuration.GetSection("WebServices:Abac"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("WebServices:Notificaciones"));

builder.Services.AddSingleton(new ConnectionStringProvider(
    builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton(new ConnectionStringProvider(
    builder.Configuration.GetConnectionString("CerberusConfig")));

builder.Services.AddSingleton(jwtSettings);

#endregion

#region R2

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
        config);
});

#endregion

#region AUTENTICACION

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
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

#endregion

#region EMPLEADOS

builder.Services.AddScoped<ListadoEmpleadosFunctions>();
builder.Services.AddScoped<EditarEmpleadoFunctions>();
builder.Services.AddScoped<AltaDomiciliosFunctions>();
builder.Services.AddScoped<ListadoDomiciliosFunctions>();
builder.Services.AddScoped<EliminadoDomicilioFunctions>();

#endregion

#region CANDIDATOS

builder.Services.AddScoped<CandidatosFunctions>();
builder.Services.AddScoped<SaludFunctions>();

#endregion

#region ARCHIVOS

builder.Services.AddScoped<FileEmpleadoService>();
builder.Services.AddScoped<FileCandidatoService>();
builder.Services.AddScoped<FileAsistenciaService>();
builder.Services.AddScoped<FileRelevoNoPlaneadoService>();

#endregion

#region NOTIFICACIONES

builder.Services.AddScoped<NotificationClient>();
builder.Services.AddScoped<ServicioNotificationFunctions>();
builder.Services.AddScoped<RelevoNotificationFunctions>();

#endregion

#region ASISTENCIAS

builder.Services.AddScoped<AsistenciasFunctions>();

#endregion

#region SUPERVISION

builder.Services.AddScoped<SupervisionFunctions>();

#endregion

#region RELEVOS NO PLANEADOS

builder.Services.AddScoped<RelevoNoPlaneadoDataService>();
builder.Services.AddScoped<RelevoIntegracionAsistenciaFunctions>();
builder.Services.AddScoped<RelevoSolicitudFunctions>();
builder.Services.AddScoped<RelevoAsignacionFunctions>();
builder.Services.AddScoped<RelevoConsultaFunctions>();
builder.Services.AddScoped<RelevoNoPlaneadoFunctions>();

#endregion

#region HTTP CLIENTS

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

builder.Services.AddHttpClient<ContratacionCandidatoFunctions>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<WsOptions>>().Value;

    http.BaseAddress = new Uri(opt.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

#endregion

#region ASP.NET

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#endregion

#region APP

var app = builder.Build();

app.UsePathBase("/Negocio");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

#endregion