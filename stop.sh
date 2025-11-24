#!/bin/bash

# Voice Agent - Stop Script

set -e

echo "🛑 Stopping Voice Agent Application..."
echo ""

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    echo "❌ docker-compose not found."
    exit 1
fi

# Stop services
docker-compose down

echo ""
echo "✅ All services stopped!"
echo ""
echo "💡 To remove volumes and networks:"
echo "   docker-compose down -v"
echo ""
echo "💡 To remove images as well:"
echo "   docker-compose down -v --rmi all"
echo ""
