@echo off
set app_dir=%1
:: detect if buildpack supports this application

if exist "%app_dir%\run.bat" (
    echo Run Buildpack
	exit /b 0
) else (
    exit /b 1
)
