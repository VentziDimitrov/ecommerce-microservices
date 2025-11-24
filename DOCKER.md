# Docker Deployment Guide

## Overview

This document provides comprehensive instructions for deploying the Voice Agent application using Docker and Docker Compose. The application consists of two services:

1. **Backend (VoiceAgentApi)**: ASP.NET Core 8.0 Web API
2. **Frontend (voice-agent-ui)**: React application served by Nginx

## Prerequisites

- Docker Engine 20.10 or higher
- Docker Compose 2.0 or higher
- At least 4GB of available RAM
- Valid API keys for:
  - Azure Speech Service
  - OpenAI API
  - Pinecone Vector Database

## Quick Start

### 1. Clone and Navigate

```bash
cd /home/ventzi/work/speech_service/ecommerce-microservices
```

### 2. Configure Environment Variables

```bash
# Copy the example environment file
cp .env.example .env

# Edit .env and add your API keys
nano .env
```

Required environment variables:
- `AZURE_SPEECH_KEY`: Your Azure Cognitive Services Speech key
- `OPENAI_API_KEY`: Your OpenAI API key
- `PINECONE_API_KEY`: Your Pinecone API key

### 3. Build and Run

```bash
# Build and start both services
docker-compose up -d

# View logs
docker-compose logs -f

# Check service status
docker-compose ps
```

### 4. Access the Application

- **Frontend UI**: http://localhost:3000
- **Backend API**: http://localhost:5000
- **Health Check (Backend)**: http://localhost:5000/health
- **Health Check (Frontend)**: http://localhost:3000/health

## Docker Compose Files

### Main Configuration

**docker-compose.yml** - Base configuration for both services

### Environment-Specific Overrides

**docker-compose.dev.yml** - Development overrides
```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up
```

**docker-compose.prod.yml** - Production overrides with resource limits
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## Service Architecture

```
┌─────────────────────────────────────────────────────┐
│                    User Browser                      │
└────────────────┬────────────────────────────────────┘
                 │
                 │ HTTP/WS
                 ▼
┌─────────────────────────────────────────────────────┐
│         Frontend (Nginx + React)                     │
│         Container: voice-agent-ui                    │
│         Port: 3000 → 80                             │
└────────────────┬────────────────────────────────────┘
                 │
                 │ API Calls
                 ▼
┌─────────────────────────────────────────────────────┐
│         Backend (ASP.NET Core API)                   │
│         Container: voice-agent-api                   │
│         Port: 5000 → 8080                           │
│                                                       │
│         External Services:                           │
│         - Azure Speech Service (STT/TTS)            │
│         - OpenAI API (LLM)                          │
│         - Pinecone (Vector DB)                      │
└─────────────────────────────────────────────────────┘
```

## Detailed Configuration

### Backend Service (voice-agent-api)

#### Ports
- `5000:8080` - Main HTTP port
- `5001:8081` - HTTPS port (dev only)

#### Environment Variables

**Azure Speech Service:**
```yaml
AZURE_SPEECH_KEY: Your Azure Speech API key
AZURE_SPEECH_REGION: germanywestcentral (or your region)
```

**OpenAI:**
```yaml
OPENAI_API_KEY: Your OpenAI API key
OPENAI_CHAT_MODEL: gpt-4
OPENAI_EMBEDDING_MODEL: text-embedding-3-small
OPENAI_TEMPERATURE: 0.2
OPENAI_MAX_TOKENS: 500
OPENAI_TOP_P: 1.0
```

**Pinecone:**
```yaml
PINECONE_API_KEY: Your Pinecone API key
PINECONE_REGION: us-east-1
PINECONE_MODEL: text-embedding-3-small
PINECONE_DIMENSION: 1536
PINECONE_NAMESPACE: __default__
PINECONE_INDEX_NAME: bulgariaair
```

**CORS:**
```yaml
Cors__AllowedOrigins__0: http://localhost
Cors__AllowedOrigins__1: http://localhost:3000
Cors__AllowedOrigins__2: http://frontend
```

#### Volumes
- `./VoiceAgentApi/logs:/app/logs` - Log files persistence
- `./VoiceAgentApi/Data:/app/Data:ro` - Knowledge base (read-only)

#### Health Check
- Endpoint: `http://localhost:8080/health`
- Interval: 30 seconds
- Timeout: 10 seconds
- Retries: 3
- Start period: 40 seconds

### Frontend Service (voice-agent-ui)

#### Ports
- `3000:80` - HTTP port served by Nginx

#### Environment Variables
```yaml
REACT_APP_API_URL: http://localhost:5000
REACT_APP_WS_URL: ws://localhost:5000/voice/ws
```

#### Health Check
- Endpoint: `http://localhost:80/health`
- Interval: 30 seconds
- Timeout: 3 seconds
- Retries: 3
- Start period: 5 seconds

#### Nginx Configuration
- Gzip compression enabled
- Security headers configured
- Static asset caching (1 year)
- React Router support (SPA)
- AudioWorklet processor served correctly

## Docker Commands Reference

### Basic Operations

```bash
# Start services
docker-compose up -d

# Stop services
docker-compose down

# Restart services
docker-compose restart

# View logs
docker-compose logs -f

# View logs for specific service
docker-compose logs -f backend
docker-compose logs -f frontend

# Check service status
docker-compose ps

# Execute command in running container
docker-compose exec backend bash
docker-compose exec frontend sh
```

### Build Operations

```bash
# Build images
docker-compose build

# Build without cache
docker-compose build --no-cache

# Build specific service
docker-compose build backend
docker-compose build frontend

# Pull and rebuild
docker-compose pull && docker-compose build
```

### Cleanup Operations

```bash
# Stop and remove containers
docker-compose down

# Remove containers, networks, and volumes
docker-compose down -v

# Remove everything including images
docker-compose down -v --rmi all

# Prune unused Docker resources
docker system prune -a
```

## Development Workflow

### Running in Development Mode

```bash
# Use development override
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up

# Enable more verbose logging
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
docker-compose logs -f backend
```

### Hot Reload (Optional)

For frontend hot reload during development:

```bash
# Modify docker-compose.dev.yml to mount source
# Then run frontend locally instead:
cd voice-agent-ui
npm install
npm start
```

### Debugging

```bash
# View container processes
docker-compose top

# Inspect container
docker inspect voice-agent-api
docker inspect voice-agent-ui

# View resource usage
docker stats voice-agent-api voice-agent-ui

# Check logs for errors
docker-compose logs backend | grep -i error
docker-compose logs frontend | grep -i error
```

## Production Deployment

### 1. Update Configuration

Edit `docker-compose.prod.yml`:

```yaml
# Update CORS allowed origins
- Cors__AllowedOrigins__0=https://yourdomain.com

# Update frontend API URL
- REACT_APP_API_URL=https://api.yourdomain.com
- REACT_APP_WS_URL=wss://api.yourdomain.com/voice/ws
```

### 2. Secure Environment Variables

Never commit `.env` file to version control. Use secrets management:

```bash
# Docker Swarm secrets (if using swarm)
docker secret create azure_speech_key /path/to/key
docker secret create openai_api_key /path/to/key

# Or use external secrets manager
# - AWS Secrets Manager
# - Azure Key Vault
# - HashiCorp Vault
```

### 3. Deploy

```bash
# Build for production
docker-compose -f docker-compose.yml -f docker-compose.prod.yml build

# Start services
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Verify health
curl http://localhost:5000/health
curl http://localhost:3000/health
```

### 4. Configure Reverse Proxy

Use Nginx or Caddy as reverse proxy:

```nginx
# Example Nginx configuration
server {
    listen 443 ssl http2;
    server_name yourdomain.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    location /voice/ws {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 86400;
    }
}
```

## Monitoring and Logging

### Viewing Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f backend

# Last 100 lines
docker-compose logs --tail=100 backend

# Since timestamp
docker-compose logs --since 2024-01-01T00:00:00 backend
```

### Log Files

Backend logs are persisted to:
```
./VoiceAgentApi/logs/voiceagent-YYYYMMDD.txt
```

### Health Monitoring

```bash
# Check health status
docker-compose ps

# Health check logs
docker inspect --format='{{json .State.Health}}' voice-agent-api | jq
docker inspect --format='{{json .State.Health}}' voice-agent-ui | jq
```

### Resource Monitoring

```bash
# Real-time stats
docker stats voice-agent-api voice-agent-ui

# Container inspect
docker inspect voice-agent-api
```

## Troubleshooting

### Backend Won't Start

**Check logs:**
```bash
docker-compose logs backend
```

**Common issues:**
1. Missing environment variables
   - Solution: Verify `.env` file has all required keys

2. Port already in use
   - Solution: Change port mapping in docker-compose.yml or stop conflicting service

3. Invalid API keys
   - Solution: Verify keys are correct and active

### Frontend Can't Connect to Backend

**Check CORS configuration:**
```bash
docker-compose logs backend | grep -i cors
```

**Verify environment variables:**
```bash
docker-compose exec frontend env | grep REACT_APP
```

**Solution:**
- Ensure CORS allows frontend origin
- Check `REACT_APP_API_URL` matches backend URL

### WebSocket Connection Fails

**Check Nginx configuration:**
```bash
docker-compose exec frontend cat /etc/nginx/conf.d/default.conf
```

**Verify WebSocket upgrade:**
- Ensure nginx passes WebSocket upgrade headers
- Check backend logs for WebSocket connections

### Out of Memory

**Increase resource limits in docker-compose.prod.yml:**
```yaml
deploy:
  resources:
    limits:
      memory: 4G
```

**Or adjust Docker daemon:**
```bash
# Edit /etc/docker/daemon.json
{
  "default-runtime": "runc",
  "default-memory": "4g"
}
```

### Container Keeps Restarting

**Check health check status:**
```bash
docker-compose ps
docker inspect voice-agent-api
```

**Increase start period:**
```yaml
healthcheck:
  start_period: 60s  # Increase from 40s
```

## Performance Optimization

### Resource Limits

Production settings in `docker-compose.prod.yml`:

**Backend:**
- CPU: 1-2 cores
- Memory: 1-2 GB

**Frontend:**
- CPU: 0.5-1 core
- Memory: 256-512 MB

### Network Optimization

Use bridge network for inter-container communication:
```yaml
networks:
  voice-agent-network:
    driver: bridge
```

### Build Optimization

Multi-stage builds reduce image size:
- Backend: SDK → Runtime (smaller final image)
- Frontend: Node build → Nginx serve (minimal image)

### Caching

```bash
# Use build cache
docker-compose build

# Bust cache when needed
docker-compose build --no-cache
```

## Security Best Practices

1. **Secrets Management**
   - Never commit `.env` to version control
   - Use Docker secrets or external vault
   - Rotate keys regularly

2. **Network Security**
   - Use internal network for inter-service communication
   - Expose only necessary ports
   - Use HTTPS/WSS in production

3. **Image Security**
   - Use official base images
   - Regular updates: `docker-compose pull`
   - Scan images: `docker scan voice-agent-api`

4. **Container Security**
   - Run as non-root user (configured in Dockerfiles)
   - Read-only file systems where possible
   - Limit capabilities

5. **CORS Configuration**
   - Restrict allowed origins in production
   - Don't use `AllowAnyOrigin`

## Backup and Recovery

### Backing Up Logs

```bash
# Archive logs
tar -czf logs-backup-$(date +%Y%m%d).tar.gz VoiceAgentApi/logs/

# Copy to remote storage
rsync -avz VoiceAgentApi/logs/ user@backup-server:/backups/voice-agent-logs/
```

### Backing Up Configuration

```bash
# Backup environment file (encrypted)
gpg -c .env

# Backup docker-compose configuration
tar -czf docker-config-backup.tar.gz docker-compose*.yml .env.example
```

### Disaster Recovery

```bash
# Stop services
docker-compose down

# Restore configuration
tar -xzf docker-config-backup.tar.gz

# Restore environment variables
gpg -d .env.gpg > .env

# Rebuild and restart
docker-compose build --no-cache
docker-compose up -d
```

## Updating the Application

### Update Backend

```bash
# Pull latest code
git pull

# Rebuild backend
docker-compose build backend

# Restart with zero downtime (if using load balancer)
docker-compose up -d --no-deps --build backend
```

### Update Frontend

```bash
# Pull latest code
git pull

# Rebuild frontend
docker-compose build frontend

# Restart
docker-compose up -d --no-deps --build frontend
```

### Update All Services

```bash
# Pull latest code
git pull

# Rebuild all
docker-compose build

# Restart all
docker-compose up -d
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Deploy

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Build images
        run: docker-compose build

      - name: Push to registry
        run: |
          docker tag voice-agent-api registry.example.com/voice-agent-api:latest
          docker push registry.example.com/voice-agent-api:latest
```

## Scaling

### Horizontal Scaling

```bash
# Scale backend (requires load balancer)
docker-compose up -d --scale backend=3

# Note: Frontend can't be scaled on same port
# Use reverse proxy for load balancing
```

### Docker Swarm (Production)

```bash
# Initialize swarm
docker swarm init

# Deploy stack
docker stack deploy -c docker-compose.yml voice-agent

# Scale services
docker service scale voice-agent_backend=3
```

## Summary

✅ **Quick Start**: `docker-compose up -d`
✅ **Development**: Use `docker-compose.dev.yml` override
✅ **Production**: Use `docker-compose.prod.yml` with proper secrets
✅ **Monitoring**: Health checks + log persistence
✅ **Security**: CORS, secrets management, HTTPS
✅ **Performance**: Resource limits, multi-stage builds

For more information, see:
- [Backend Refactoring Documentation](VoiceAgentApi/REFACTORING.md)
- [Frontend Architecture](voice-agent-ui/ARCHITECTURE.md)
