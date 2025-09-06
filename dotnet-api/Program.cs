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
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("");
        return;
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/documents", async (HttpRequest req, IHttpClientFactory http, AppDbContext db, IAmazonS3 s3Client) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files["file"];
    if (file == null) return Results.BadRequest(new { error = "missing file" });

    var doc = new Document { Title = file.FileName, Status = "uploading", CreatedAt = DateTime.UtcNow };
    db.Documents.Add(doc);
    await db.SaveChangesAsync();

    try
    {
        // Create bucket if it doesn't exist
        var bucketName = "documents";
        try
        {
            await s3Client.GetBucketLocationAsync(bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
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

        // Update document with file URL
        var fileUrl = $"minio://{bucketName}/{objectName}";
        doc.FileUrl = fileUrl;
        doc.Status = "queued";
        await db.SaveChangesAsync();

        // Process document with Python service
        var client = http.CreateClient("py");
        await client.PostAsJsonAsync("/process/extract", new { documentId = doc.Id, fileUrl });

        return Results.Ok(new { documentId = doc.Id });
    }
    catch (Exception ex)
    {
        doc.Status = "error";
        await db.SaveChangesAsync();
        return Results.BadRequest(new { error = $"Upload failed: {ex.Message}" });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
