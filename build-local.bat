@echo off
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set PROJECT_ROOT=e:\ai\betterCommunication
set OUTPUT_DIR=%PROJECT_ROOT%\build
set OUTPUT_DLL=%OUTPUT_DIR%\BetterCommunication.dll

set GAME_MANAGED=C:\Progra~2\Steam\steamapps\common\SUPERB~1\SUPERB~1\Managed
set BEPINEX_CORE=C:\Users\marki\AppData\Roaming\THUNDE~1\DataFolder\SuperBattleGolf\profiles\Default\BepInEx\core
set PLUGIN_DIR=C:\Users\marki\AppData\Roaming\THUNDE~1\DataFolder\SuperBattleGolf\profiles\Default\BepInEx\plugins\marki-BetterCommunication

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"

%CSC% /nologo /target:library /langversion:5 ^
 /out:%OUTPUT_DLL% ^
 /reference:%BEPINEX_CORE%\BepInEx.dll,%GAME_MANAGED%\GameAssembly.dll,%GAME_MANAGED%\UnityEngine.dll,%GAME_MANAGED%\UnityEngine.CoreModule.dll,%GAME_MANAGED%\UnityEngine.IMGUIModule.dll,%GAME_MANAGED%\UnityEngine.InputLegacyModule.dll,%GAME_MANAGED%\UnityEngine.UIModule.dll,%GAME_MANAGED%\UnityEngine.UI.dll,%GAME_MANAGED%\Unity.TextMeshPro.dll,%GAME_MANAGED%\netstandard.dll ^
 %PROJECT_ROOT%\src\Plugin.cs ^
 %PROJECT_ROOT%\src\ChatHistoryManager.cs ^
 %PROJECT_ROOT%\src\ChatHistoryMessageTracker.cs ^
 %PROJECT_ROOT%\src\ChatLinkTextHandler.cs ^
 %PROJECT_ROOT%\src\SteamProfileClickTarget.cs ^
 %PROJECT_ROOT%\src\SteamProfileLinkInjector.cs

if errorlevel 1 exit /b 1

copy /y "%OUTPUT_DLL%" "%PLUGIN_DIR%\BetterCommunication.dll" >nul
if errorlevel 1 exit /b 1
copy /y "%PROJECT_ROOT%\manifest.json" "%PLUGIN_DIR%\manifest.json" >nul
if errorlevel 1 exit /b 1
copy /y "%PROJECT_ROOT%\README.md" "%PLUGIN_DIR%\README.md" >nul
if errorlevel 1 exit /b 1
echo Built and copied to %PLUGIN_DIR%
