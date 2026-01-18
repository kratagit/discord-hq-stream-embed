@echo off
:: Ustaw folder roboczy na ten, w którym jest plik .bat (ważne!)
cd /d "%~dp0"

:: Uruchom skrypt używając pythona z wirtualnego środowiska
start "" ".venv\Scripts\python.exe" embed_discord.py

exit