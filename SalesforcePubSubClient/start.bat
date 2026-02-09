@echo off
REM Salesforce Pub/Sub API Client - Quick Start Script for Windows
REM ASP.NET Core version with AddGrpcClient

echo ==============================================
echo Salesforce Pub/Sub API ASP.NET Core Client
echo ==============================================
echo.

REM Check if .env file exists
if not exist .env (
    echo Creating .env file from template...
    copy .env.example .env
    echo.
    echo IMPORTANT: Please edit the .env file with your Salesforce credentials
    echo    Required fields:
    echo    - SF_INSTANCE_URL
    echo    - SF_ACCESS_TOKEN
    echo    - SF_TENANT_ID
    echo    - SF_TOPIC_NAME
    echo.
    pause
)

REM Load environment variables from .env
for /f "tokens=*" %%a in ('type .env ^| findstr /v "^#" ^| findstr /v "^$"') do set %%a

REM Validate required environment variables
if "%SF_INSTANCE_URL%"=="" (
    echo Error: SF_INSTANCE_URL is not set
    pause
    exit /b 1
)

if "%SF_ACCESS_TOKEN%"=="" (
    echo Error: SF_ACCESS_TOKEN is not set
    pause
    exit /b 1
)

if "%SF_TENANT_ID%"=="" (
    echo Error: SF_TENANT_ID is not set
    pause
    exit /b 1
)

if "%SF_TOPIC_NAME%"=="" (
    echo Error: SF_TOPIC_NAME is not set
    pause
    exit /b 1
)

echo Configuration:
echo   Instance URL: %SF_INSTANCE_URL%
echo   Tenant ID: %SF_TENANT_ID%
echo   Topic: %SF_TOPIC_NAME%
if "%SF_REPLAY_PRESET%"=="" (
    echo   Replay Preset: LATEST
) else (
    echo   Replay Preset: %SF_REPLAY_PRESET%
)
if "%SF_PUBSUB_ENDPOINT%"=="" (
    echo   Endpoint: api.pubsub.salesforce.com:443
) else (
    echo   Endpoint: %SF_PUBSUB_ENDPOINT%
)
echo.

REM Restore dependencies
echo Restoring NuGet packages...
dotnet restore

if %ERRORLEVEL% neq 0 (
    echo Error: Failed to restore packages
    pause
    exit /b 1
)

echo.

REM Build the project
echo Building the project...
dotnet build

if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build project
    pause
    exit /b 1
)

echo.
echo Starting Salesforce Pub/Sub subscriber (ASP.NET Core)...
echo    Press Ctrl+C to stop
echo.

REM Run the application
dotnet run