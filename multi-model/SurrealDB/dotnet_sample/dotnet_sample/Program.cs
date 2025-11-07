using DotnetSample.Services;
using DotnetSample.Configuration;
using SurrealDb.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Ollama 설정
builder.Services.Configure<OllamaSettings>(
    builder.Configuration.GetSection("Ollama"));

// SurrealDB
builder.Services.AddSurreal(builder.Configuration.GetConnectionString("SurrealDB")!);
builder.Services.AddScoped<SurrealDbService>();

// 서비스 등록
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddScoped<VectorSearchService>();

var app = builder.Build();

// 초기화
using (var scope = app.Services.CreateScope())
{
    var surrealClient = scope.ServiceProvider.GetRequiredService<ISurrealDbClient>();
    await surrealClient.Use("test", "test");

    var vectorSearchService = scope.ServiceProvider.GetRequiredService<VectorSearchService>();
    await vectorSearchService.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();