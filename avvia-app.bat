@echo off
rem Avvia l'app con Expo. Con "tunnel" funziona anche se il telefono non e' sulla stessa rete Wi-Fi:
rem   avvia-app.bat tunnel
cd /d "%~dp0app" || goto :errore

if not exist node_modules (
    call npm install || goto :errore
)

if /i "%~1"=="tunnel" (
    call npx expo start --tunnel
) else (
    call npx expo start
)
if errorlevel 1 goto :errore
exit /b 0

:errore
echo.
echo Avvio non riuscito.
pause
exit /b 1
