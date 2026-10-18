@echo off
setlocal

set "VERSION=99.99.99-local"
set "OUTPUT=.\artifacts\packages"
set "CONFIGURATION=Release"

:parse_args
if "%~1"=="" goto args_done

if /i "%~1"=="-c" (
    set "CONFIGURATION=%~2"
    shift
    shift
    goto parse_args
)

echo Unknown argument: %~1
exit /b 1

:args_done
if not exist "%OUTPUT%" mkdir "%OUTPUT%"

for %%P in (
    "src/CachedEfCore/CachedEfCore.csproj"
    "src/CachedEfCore.Caching.FusionCache/CachedEfCore.Caching.FusionCache.csproj"
    "src/CachedEfCore.Caching.InMemory/CachedEfCore.Caching.InMemory.csproj"
    "src/CachedEfCore.DependencyInjection/CachedEfCore.DependencyInjection.csproj"
    "src/CachedEfCore.SqlServer/CachedEfCore.SqlServer.csproj"
    "src/CachedEfCore.PostgreSQL/CachedEfCore.PostgreSQL.csproj"
) do (
    echo.
    echo Packing %%~P - Configuration %CONFIGURATION% - Version %VERSION%

    dotnet pack "%%~P" -c "%CONFIGURATION%" -o "%OUTPUT%" -p:PackageVersion=%VERSION%

    if errorlevel 1 (
        echo.
        echo ERROR: Failed to pack %%~P
        exit /b 1
    )
)

echo.
echo All packages created successfully in "%OUTPUT%".
exit /b 0