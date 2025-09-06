# Multi-stage build for production deployment
FROM node:18-alpine AS frontend-build

# Set working directory
WORKDIR /app/frontend

# Copy package files
COPY frontend/package*.json ./

# Install dependencies
RUN npm ci --only=production

# Copy source code
COPY frontend/ .

# Build the React app
RUN npm run build

# .NET API stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS dotnet-api

WORKDIR /app

# Copy .NET API files
COPY dotnet-api/ .

# Restore dependencies
RUN dotnet restore

# Publish the application
RUN dotnet publish -c Release -o out

# Python processor stage
FROM python:3.11-slim AS py-processor

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-eng \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install Python dependencies
COPY py-processor/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy Python application
COPY py-processor/ .

# Final stage - Nginx for serving
FROM nginx:alpine

# Install Node.js for running the frontend
RUN apk add --no-cache nodejs npm

# Copy built frontend
COPY --from=frontend-build /app/frontend/build /usr/share/nginx/html

# Copy .NET API
COPY --from=dotnet-api /app/out /app/dotnet-api

# Copy Python processor
COPY --from=py-processor /app /app/py-processor

# Install .NET runtime and curl
RUN apk add --no-cache icu-libs curl
COPY --from=mcr.microsoft.com/dotnet/aspnet:8.0 /usr/share/dotnet /usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"

# Install Python
RUN apk add --no-cache python3 py3-pip

# Copy nginx configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 80

# Start script
COPY start.sh /start.sh
RUN chmod +x /start.sh

CMD ["/start.sh"]
