#!/bin/sh

# Start .NET API in background
cd /app/dotnet-api
dotnet dotnet-api.dll &
DOTNET_PID=$!

# Start Python processor in background
cd /app/py-processor
python -m uvicorn app.main:app --host 0.0.0.0 --port 8000 &
PYTHON_PID=$!

# Start Nginx in foreground
nginx -g "daemon off;" &
NGINX_PID=$!

# Function to handle shutdown
shutdown() {
    echo "Shutting down services..."
    kill $DOTNET_PID $PYTHON_PID $NGINX_PID
    wait
    exit 0
}

# Set up signal handlers
trap shutdown SIGTERM SIGINT

# Wait for any process to exit
wait
