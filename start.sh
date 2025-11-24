#!/bin/bash

# Voice Agent - Quick Start Script

set -e

echo "🚀 Starting Voice Agent Application..."
echo ""

# Check if .env exists
if [ ! -f .env ]; then
    echo "⚠️  .env file not found!"
    echo "Creating .env from .env.example..."
    cp .env.example .env
    echo ""
    echo "⚠️  IMPORTANT: Edit .env file and add your API keys before proceeding!"
    echo ""
    read -p "Press Enter after updating .env file, or Ctrl+C to cancel..."
fi

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    echo "❌ docker-compose not found. Please install docker-compose."
    exit 1
fi

# Determine environment
ENV=${1:-dev}

echo "📦 Environment: $ENV"
echo ""

# Build and start services based on environment
case $ENV in
    dev|development)
        echo "🔨 Building and starting services in DEVELOPMENT mode..."
        docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
        ;;
    prod|production)
        echo "🔨 Building and starting services in PRODUCTION mode..."
        docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
        ;;
    *)
        echo "🔨 Building and starting services in DEFAULT mode..."
        docker-compose up -d --build
        ;;
esac

echo ""
echo "⏳ Waiting for services to be healthy..."
sleep 5

# Wait for backend health check
echo "Checking backend health..."
for i in {1..30}; do
    if curl -f http://localhost:5000/health > /dev/null 2>&1; then
        echo "✅ Backend is healthy!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "⚠️  Backend health check timeout. Check logs: docker-compose logs backend"
    fi
    sleep 2
done

# Wait for frontend health check
echo "Checking frontend health..."
for i in {1..20}; do
    if curl -f http://localhost:3000/health > /dev/null 2>&1; then
        echo "✅ Frontend is healthy!"
        break
    fi
    if [ $i -eq 20 ]; then
        echo "⚠️  Frontend health check timeout. Check logs: docker-compose logs frontend"
    fi
    sleep 2
done

echo ""
echo "✅ Voice Agent Application is running!"
echo ""
echo "📍 Access points:"
echo "   Frontend UI:  http://localhost:3000"
echo "   Backend API:  http://localhost:5000"
echo "   Backend Health: http://localhost:5000/health"
echo ""
echo "📊 View logs:"
echo "   docker-compose logs -f"
echo ""
echo "🛑 Stop services:"
echo "   docker-compose down"
echo ""
