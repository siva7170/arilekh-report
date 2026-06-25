using ArilekhReport.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ReportSessionService is Singleton — holds rendered documents in memory by GUID
builder.Services.AddSingleton<ReportSessionService>();

// CORS — allow Angular dev server and production origins
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(
        "http://localhost:4200",   // Angular dev
        "http://localhost:4300",
        "https://your-angular-app.example.com"  // replace with production origin
    )
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
