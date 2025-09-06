using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

// FORCE REDEPLOY - SIMPLIFIED VERSION v3 - NO DATABASE

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
// HTTP client removed for simplified approach

// MinIO service removed for simplified approach

// Database completely removed for simplified approach

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

app.MapPost("/documents", async (HttpRequest req, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("=== SIMPLIFIED DOCUMENTS ENDPOINT - NO DATABASE ===");
        
        var form = await req.ReadFormAsync();
        var file = form.Files["file"];
        if (file == null) 
        {
            logger.LogWarning("No file provided in request");
            return Results.BadRequest(new { error = "missing file" });
        }

        logger.LogInformation($"Processing file: {file.FileName}, Size: {file.Length}");

        // Generate a simple document ID without database
        var docId = Guid.NewGuid().ToString("N")[..8];
        
        logger.LogInformation($"Created document with ID: {docId}");

        return Results.Ok(new { 
            documentId = docId, 
            message = "Document uploaded successfully - SIMPLIFIED VERSION",
            fileName = file.FileName,
            size = file.Length,
            timestamp = DateTime.UtcNow
        });
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
app.MapGet("/test", () => Results.Ok(new { message = "CORS test successful - SIMPLIFIED VERSION v3", timestamp = DateTime.UtcNow }));

// Simple file upload test endpoint
app.MapPost("/upload-test", async (HttpRequest req) =>
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
            contentType = file.ContentType,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

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

// Debug endpoint simplified
app.MapGet("/debug", () => Results.Ok(new { 
    message = "Simplified API - No database or external services", 
    timestamp = DateTime.UtcNow 
}));

// Database endpoints removed for simplified approach

app.Run();
