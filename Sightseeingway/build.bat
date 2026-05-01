@echo off
setlocal

set "configuration=%~1"
if "%configuration%"=="" set "configuration=Release"

echo Building Sightseeingway (%configuration%)...
dotnet build "Sightseeingway.csproj" --configuration %configuration%
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo Build complete.
echo DalamudPackager output: bin\%configuration%\Sightseeingway\latest.zip
