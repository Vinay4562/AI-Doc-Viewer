using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using DotnetApi.Models;
using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);

// Basic services, DB and HTTP client for Python service
builder.Services.AddControllers().AddJsonOptions(o => {
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://ai-doc-viewer-frontend.onrender.com",
                "https://ai-doc-viewer-processor.onrender.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddHttpClient("py", c => c.BaseAddress = new Uri(builder.Configuration["PY_BASE"] ?? "http://py-processor:8000"));

// Add MinIO service (using AWS S3 SDK for MinIO compatibility)
builder.Services.AddSingleton<IAmazonS3>(provider =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = $"http://{builder.Configuration["MINIO_ENDPOINT"] ?? "minio:9000"}",
        ForcePathStyle = true,
        UseHttp = true
    };
    
    return new AmazonS3Client(
        builder.Configuration["MINIO_ACCESS_KEY"] ?? "minioadmin",
        builder.Configuration["MINIO_SECRET_KEY"] ?? "minioadmin",
        config);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Enable CORS
app.UseCors("AllowFrontend");

// Handle OPTIONS requests for CORS preflight
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        context.Response.StatusCode = 200;
        return;
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Simplified document upload endpoint
app.MapPost("/documents", async (IFormFile file, AppDbContext db, IAmazonS3 s3) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded");

    var document = new Document
    {
        FileName = file.FileName,
        Status = "uploaded",
        UploadedAt = DateTime.UtcNow
    };

    db.Documents.Add(document);
    await db.SaveChangesAsync();

    // For simplified version, just return success
    return Results.Ok(new { id = document.Id, message = "Document uploaded successfully (simplified version)" });
});

app.Run();
