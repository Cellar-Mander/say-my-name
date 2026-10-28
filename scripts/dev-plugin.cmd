@echo off
rem Runs scripts\dev-plugin.ps1, bypassing the execution policy that blocks unsigned scripts by default
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-plugin.ps1" %*
exit /b %ERRORLEVEL%
