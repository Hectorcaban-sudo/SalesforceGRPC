#!/bin/bash

# Salesforce Pub/Sub API Client - Quick Start Script
# This script helps you get started with the subscriber

echo "=============================================="
echo "Salesforce Pub/Sub API C# Client"
echo "=============================================="
echo ""

# Check if .env file exists
if [ ! -f .env ]; then
    echo "Creating .env file from template..."
    cp .env.example .env
    echo ""
    echo "⚠️  IMPORTANT: Please edit the .env file with your Salesforce credentials"
    echo "   Required fields:"
    echo "   - SF_INSTANCE_URL"
    echo "   - SF_ACCESS_TOKEN"
    echo "   - SF_TENANT_ID"
    echo "   - SF_TOPIC_NAME"
    echo ""
    read -p "Press Enter after you've configured .env file..."
fi

# Load environment variables from .env
if [ -f .env ]; then
    export $(grep -v '^#' .env | xargs)
fi

# Validate required environment variables
if [ -z "$SF_INSTANCE_URL" ]; then
    echo "❌ Error: SF_INSTANCE_URL is not set"
    exit 1
fi

if [ -z "$SF_ACCESS_TOKEN" ]; then
    echo "❌ Error: SF_ACCESS_TOKEN is not set"
    exit 1
fi

if [ -z "$SF_TENANT_ID" ]; then
    echo "❌ Error: SF_TENANT_ID is not set"
    exit 1
fi

if [ -z "$SF_TOPIC_NAME" ]; then
    echo "❌ Error: SF_TOPIC_NAME is not set"
    exit 1
fi

echo "Configuration:"
echo "  Instance URL: $SF_INSTANCE_URL"
echo "  Tenant ID: $SF_TENANT_ID"
echo "  Topic: $SF_TOPIC_NAME"
echo "  Replay Preset: ${SF_REPLAY_PRESET:-LATEST}"
echo "  Endpoint: ${SF_PUBSUB_ENDPOINT:-api.pubsub.salesforce.com:443}"
echo ""

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore

if [ $? -ne 0 ]; then
    echo "❌ Error: Failed to restore packages"
    exit 1
fi

echo ""

# Build the project
echo "Building the project..."
dotnet build

if [ $? -ne 0 ]; then
    echo "❌ Error: Failed to build project"
    exit 1
fi

echo ""
echo "✅ Starting Salesforce Pub/Sub subscriber..."
echo "   Press Ctrl+C to stop"
echo ""

# Run the application
dotnet run