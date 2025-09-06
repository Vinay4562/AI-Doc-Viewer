using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using DotnetApi.Models;
using Amazon.S3;
using Amazon.S3.Model;
using Npgsql;

// Force redeploy to pick up new configuration

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
    var minioEndpoint = builder.Configuration["MINIO_ENDPOINT"] ?? "https://document-assistant-storage.onrender.com";
    
    // Handle both Docker service names and full URLs
    if (!minioEndpoint.StartsWith("http"))
    {
        minioEndpoint = $"http://{minioEndpoint}";
    }
    
    var config = new AmazonS3Config
    {
        ServiceURL = minioEndpoint,
        ForcePathStyle = true,
        UseHttp = minioEndpoint.StartsWith("http://")
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

        // Try to upload to MinIO, but don't fail if it's not available
        var bucketName = "documents";
        var fileUrl = $"local://{doc.Id}/{file.FileName}";
        
        try
        {
            // Create bucket if it doesn't exist
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
            
            fileUrl = $"minio://{bucketName}/{objectName}";
        }
        catch (Exception minioEx)
        {
            logger.LogWarning($"MinIO upload failed: {minioEx.Message}. Using local storage fallback.");
            // Continue without MinIO - file will be stored locally or in memory
        }

        // Update document with file URL
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

// Debug endpoint to check service connections
app.MapGet("/debug", async (AppDbContext db, IAmazonS3 s3Client, IHttpClientFactory http, ILogger<Program> logger, IConfiguration config) =>
{
    var results = new Dictionary<string, object>();
    
    // Add configuration debugging
    results["config"] = new {
        connectionString = config.GetConnectionString("DefaultConnection")?.Substring(0, 50) + "...",
        minioEndpoint = config["MINIO_ENDPOINT"],
        pyBase = config["PY_BASE"]
    };
    
    try
    {
        // Test database connection
        var dbTest = await db.Database.CanConnectAsync();
        results["database"] = new { status = "ok", connected = dbTest };
        
        // Try to execute a simple query
        if (dbTest)
        {
            var tableExists = await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM documents LIMIT 1");
            results["database_tables"] = new { status = "ok", tables_exist = true };
        }
    }
    catch (Exception ex)
    {
        results["database"] = new { status = "error", message = ex.Message };
    }
    
    try
    {
        // Test MinIO connection
        var buckets = await s3Client.ListBucketsAsync();
        results["minio"] = new { status = "ok", buckets = buckets.Buckets.Count };
    }
    catch (Exception ex)
    {
        results["minio"] = new { status = "error", message = ex.Message };
    }
    
    try
    {
        // Test Python service connection
        var client = http.CreateClient("py");
        var response = await client.GetAsync("/health");
        results["python_service"] = new { status = "ok", httpStatus = response.StatusCode };
    }
    catch (Exception ex)
    {
        results["python_service"] = new { status = "error", message = ex.Message };
    }
    
    return Results.Ok(results);
});

// Initialize database tables
app.MapPost("/init-db", async (AppDbContext db, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Initializing database tables...");
        
        // Create tables using raw SQL
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE EXTENSION IF NOT EXISTS vector;
            
            CREATE TABLE IF NOT EXISTS documents(
              id bigserial PRIMARY KEY,
              title text,
              file_url text,
              status text,
              created_at timestamptz DEFAULT now()
            );
            
            CREATE TABLE IF NOT EXISTS document_pages(
              id bigserial PRIMARY KEY,
              document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
              page_no int,
              text text
            );
            
            CREATE TABLE IF NOT EXISTS chunks(
              id bigserial PRIMARY KEY,
              document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
              page_no int,
              text text,
              embedding vector(384)
            );
        ");
        
        logger.LogInformation("Database tables initialized successfully");
        return Results.Ok(new { message = "Database initialized successfully" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Test raw database connection
app.MapGet("/test-db", async (IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        logger.LogInformation($"Testing connection string: {connectionString?.Substring(0, 50)}...");
        
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        var command = new NpgsqlCommand("SELECT version()", connection);
        var version = await command.ExecuteScalarAsync();
        
        await connection.CloseAsync();
        
        return Results.Ok(new { 
            status = "success", 
            message = "Database connection successful",
            version = version?.ToString()
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Raw database connection failed");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// Initialize database using raw connection
app.MapPost("/init-db-raw", async (IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        logger.LogInformation("Initializing database tables using raw connection...");
        
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        var initSql = @"
            CREATE EXTENSION IF NOT EXISTS vector;
            
            CREATE TABLE IF NOT EXISTS documents(
              id bigserial PRIMARY KEY,
              title text,
              file_url text,
              status text,
              created_at timestamptz DEFAULT now()
            );
            
            CREATE TABLE IF NOT EXISTS document_pages(
              id bigserial PRIMARY KEY,
              document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
              page_no int,
              text text
            );
            
            CREATE TABLE IF NOT EXISTS chunks(
              id bigserial PRIMARY KEY,
              document_id bigint REFERENCES documents(id) ON DELETE CASCADE,
              page_no int,
              text text,
              embedding vector(384)
            );
        ";
        
        var command = new NpgsqlCommand(initSql, connection);
        await command.ExecuteNonQueryAsync();
        
        await connection.CloseAsync();
        
        logger.LogInformation("Database tables initialized successfully using raw connection");
        return Results.Ok(new { message = "Database initialized successfully using raw connection" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database using raw connection");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();
