#!/bin/bash

# Voice Agent - Logs Script

SERVICE=${1:-""}

if [ -z "$SERVICE" ]; then
    echo "📊 Showing logs for all services..."
    echo "   (Ctrl+C to stop)"
    echo ""
    docker-compose logs -f
else
    echo "📊 Showing logs for $SERVICE..."
    echo "   (Ctrl+C to stop)"
    echo ""
    docker-compose logs -f $SERVICE
fi
