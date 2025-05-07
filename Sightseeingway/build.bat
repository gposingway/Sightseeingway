@echo off
setlocal enabledelayedexpansion

echo Sightseeingway Build Script
echo ===========================
echo.

:: Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo Error: dotnet command not found. Please ensure .NET SDK is installed.
    exit /b 1
)

:: Set default build configuration
set "configuration=Release"

:: Process command line arguments
if "%1"=="debug" (
    set "configuration=Debug"
) else if "%1"=="release" (
    set "configuration=Release"
) else if not "%1"=="" (
    echo Unknown build configuration: %1
    echo Usage: build.bat [debug^|release]
    echo.
    echo Defaulting to Release build...
    timeout /t 3 >nul
)

echo Building Sightseeingway in %configuration% mode...
echo.

:: Restore NuGet packages
echo Restoring packages...
dotnet restore "Sightseeingway.csproj"
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to restore packages.
    exit /b %ERRORLEVEL%
)

:: Build the project
echo Building project...
dotnet build "Sightseeingway.csproj" --configuration %configuration% --no-restore
if %ERRORLEVEL% neq 0 (
    echo Error: Build failed.
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully!
echo Output directory: bin\x64\%configuration%

:: Check if release build and offer to create a release zip
if "%configuration%"=="Release" (
    echo.
    set /p "createZip=Create release zip package? (Y/N): "
    if /i "!createZip!"=="Y" (
        echo Creating release package...
        if not exist "bin\x64\Release\Sightseeingway" mkdir "bin\x64\Release\Sightseeingway"
        copy "bin\x64\Release\Sightseeingway.dll" "bin\x64\Release\Sightseeingway\" >nul
        copy "bin\x64\Release\Sightseeingway.json" "bin\x64\Release\Sightseeingway\" >nul
        
        :: Check if 7zip is available
        where 7z >nul 2>nul
        if %ERRORLEVEL% neq 0 (
            echo Warning: 7zip not found. Please manually zip the files from bin\x64\Release\Sightseeingway\
        ) else (
            7z a -tzip "bin\x64\Release\Sightseeingway\latest.zip" "bin\x64\Release\Sightseeingway\Sightseeingway.dll" "bin\x64\Release\Sightseeingway\Sightseeingway.json" >nul
            if %ERRORLEVEL% neq 0 (
                echo Error: Failed to create zip file.
            ) else (
                echo Release package created at bin\x64\Release\Sightseeingway\latest.zip
            )
        )
    )
)

echo.
pause