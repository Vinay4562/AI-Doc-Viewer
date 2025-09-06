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
                "https://ai-doc-viewer-api.onrender.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    
    // Add a more permissive policy for development
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
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

// Enable CORS first
app.UseCors("AllowFrontend");

// Add explicit CORS headers for preflight requests
app.Use(async (context, next) =>
{
    // Add CORS headers to all responses
    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
    
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("");
        return;
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/documents", async (HttpRequest req, IHttpClientFactory http, AppDbContext db, IAmazonS3 s3Client, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Received document upload request");
        
        var form = await req.ReadFormAsync();
        var file = form.Files["file"];
        if (file == null) 
        {
            logger.LogWarning("No file provided in request");
            return Results.BadRequest(new { error = "missing file" });
        }

        logger.LogInformation($"Processing file: {file.FileName}, Size: {file.Length}");

        var doc = new Document { Title = file.FileName, Status = "uploading", CreatedAt = DateTime.UtcNow };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        logger.LogInformation($"Created document with ID: {doc.Id}");

        // Create bucket if it doesn't exist
        var bucketName = "documents";
        try
        {
            await s3Client.GetBucketLocationAsync(bucketName);
            logger.LogInformation("Bucket exists");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogInformation("Creating bucket");
            await s3Client.PutBucketAsync(bucketName);
        }

        // Upload file to MinIO
        var objectName = $"{doc.Id}/{file.FileName}";
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectName,
            InputStream = file.OpenReadStream(),
            ContentType = file.ContentType
        };
        
        await s3Client.PutObjectAsync(putRequest);
        logger.LogInformation($"File uploaded to MinIO: {objectName}");

        // Update document with file URL
        var fileUrl = $"minio://{bucketName}/{objectName}";
        doc.FileUrl = fileUrl;
        doc.Status = "queued";
        await db.SaveChangesAsync();

        // Process document with Python service
        try
        {
            var client = http.CreateClient("py");
            var response = await client.PostAsJsonAsync("/process/extract", new { documentId = doc.Id, fileUrl });
            logger.LogInformation($"Python service response: {response.StatusCode}");
        }
        catch (Exception pyEx)
        {
            logger.LogWarning($"Python service call failed: {pyEx.Message}");
            // Don't fail the entire request if Python service is down
        }

        return Results.Ok(new { documentId = doc.Id, message = "Document uploaded successfully" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing document upload");
        return Results.Problem(
            detail: $"Upload failed: {ex.Message}",
            statusCode: 500,
            title: "Upload Error"
        );
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Test endpoint for CORS debugging
app.MapGet("/test", () => Results.Ok(new { message = "CORS test successful", timestamp = DateTime.UtcNow }));

// Simple upload test without database/MinIO
app.MapPost("/test-upload", async (HttpRequest req) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        var file = form.Files["file"];
        if (file == null) 
        {
            return Results.BadRequest(new { error = "No file provided" });
        }
        
        return Results.Ok(new { 
            message = "File received successfully", 
            fileName = file.FileName, 
            size = file.Length,
            contentType = file.ContentType 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();
