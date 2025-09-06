# 🚀 Deployment Guide for AI-Doc-Viewer

This guide will help you deploy the AI-Powered Document Assistant to Render.

## 📋 Prerequisites

1. **GitHub Repository**: Your code should be pushed to GitHub
2. **Render Account**: Sign up at [render.com](https://render.com)
3. **API Keys**: Get your Google Gemini API key

## 🔧 Environment Variables Setup

You'll need to set up these environment variables in Render:

### Required Environment Variables:
- `GEMINI_API_KEY`: Your Google Gemini API key
- `MINIO_ENDPOINT`: MinIO endpoint (for file storage)
- `MINIO_ACCESS_KEY`: MinIO access key
- `MINIO_SECRET_KEY`: MinIO secret key

## 🏗️ Deployment Steps

### Option 1: Individual Services (Recommended)

#### 1. Deploy Frontend (Static Site)
1. Go to Render Dashboard
2. Click "New +" → "Static Site"
3. Connect your GitHub repository
4. Configure:
   - **Name**: `ai-doc-viewer-frontend`
   - **Build Command**: `cd frontend && npm install && npm run build`
   - **Publish Directory**: `frontend/build`
   - **Environment Variables**:
     - `REACT_APP_API_URL`: `https://ai-doc-viewer-api.onrender.com`
     - `REACT_APP_PROCESSOR_URL`: `https://ai-doc-viewer-processor.onrender.com`

#### 2. Deploy .NET API
1. Click "New +" → "Web Service"
2. Connect your GitHub repository
3. Configure:
   - **Name**: `ai-doc-viewer-api`
   - **Environment**: `Docker`
   - **Dockerfile Path**: `dotnet-api/Dockerfile`
   - **Environment Variables**:
     - `ASPNETCORE_ENVIRONMENT`: `Production`
     - `PY_BASE`: `https://ai-doc-viewer-processor.onrender.com`
     - Add MinIO credentials

#### 3. Deploy Python Processor
1. Click "New +" → "Web Service"
2. Connect your GitHub repository
3. Configure:
   - **Name**: `ai-doc-viewer-processor`
   - **Environment**: `Python`
   - **Build Command**: `cd py-processor && pip install -r requirements.txt`
   - **Start Command**: `cd py-processor && python -m uvicorn app.main:app --host 0.0.0.0 --port $PORT`
   - **Environment Variables**:
     - `GEMINI_API_KEY`: Your API key
     - Add database and Redis URLs

#### 4. Create Database
1. Click "New +" → "PostgreSQL"
2. Name: `ai-doc-viewer-db`
3. Note the connection string for your services

#### 5. Create Redis
1. Click "New +" → "Redis"
2. Name: `ai-doc-viewer-redis`
3. Note the connection string for your services

### Option 2: Single Docker Service

1. Click "New +" → "Web Service"
2. Connect your GitHub repository
3. Configure:
   - **Name**: `ai-doc-viewer`
   - **Environment**: `Docker`
   - **Dockerfile Path**: `Dockerfile`
   - **Environment Variables**: Add all required variables

## 🔗 Service URLs

After deployment, your services will be available at:
- **Frontend**: `https://ai-doc-viewer-frontend.onrender.com`
- **API**: `https://ai-doc-viewer-api.onrender.com`
- **Processor**: `https://ai-doc-viewer-processor.onrender.com`

## 🛠️ Configuration Files

The repository includes several configuration files for different deployment scenarios:

- `render.yaml`: Complete multi-service configuration
- `render-frontend.yaml`: Frontend-only configuration
- `render-api.yaml`: .NET API configuration
- `render-processor.yaml`: Python processor configuration
- `Dockerfile`: Single-container deployment
- `docker-compose.yml`: Local development
- `render-docker-compose.yml`: Production Docker Compose

## 🔍 Troubleshooting

### Common Issues:

1. **Build Failures**: Check that all dependencies are properly specified
2. **Environment Variables**: Ensure all required variables are set
3. **CORS Issues**: Update CORS settings in your API services
4. **Database Connection**: Verify database URLs and credentials

### Health Checks:

- Frontend: `https://your-frontend-url.onrender.com`
- API: `https://your-api-url.onrender.com/health`
- Processor: `https://your-processor-url.onrender.com/health`

## 📊 Monitoring

Monitor your services through the Render dashboard:
- View logs for each service
- Monitor resource usage
- Check deployment status
- Set up alerts for downtime

## 🔄 Updates

To update your deployment:
1. Push changes to your GitHub repository
2. Render will automatically redeploy your services
3. Monitor the deployment logs for any issues

## 💡 Tips

1. **Free Tier Limitations**: Render's free tier has some limitations (sleep after inactivity, limited resources)
2. **Environment Variables**: Keep sensitive data in environment variables, not in code
3. **Database Migrations**: Run database migrations as part of your deployment process
4. **Logging**: Use proper logging to debug issues in production

---

**Need Help?** Check the [Render Documentation](https://render.com/docs) or create an issue in this repository.
