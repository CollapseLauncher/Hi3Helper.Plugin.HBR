@echo off
set _7zFast="%programfiles%\7-Zip-Zstandard\7z.exe"
set _7z="%programfiles%\7-Zip\7z.exe"
set _pluginName=Hi3Helper.Plugin.HBR

set currentPath=%~dp0
set projectPath=%currentPath%%_pluginName%
set indexerPath=%currentPath%Indexer
set projectPublishPath=%projectPath%\publish
set indexerPublishPath=%indexerPath%\Compiled
set indexerToolPath=%indexerPublishPath%\Indexer.exe
set thread=%NUMBER_OF_PROCESSORS%
set args1=%1

:SevenZipCheck
if exist %_7zFast% (
	set sevenzip=%_7zFast%
) else if exist %_7z% (
	set sevenzip=%_7z%
) else (
	cls
	echo 7-Zip ^(x64^) does not exist!
	echo Path: %_7z%
	echo Please download it from here: https://www.7-zip.org/
	pause | echo Press any key to retry...
	goto :SevenZipCheck
)

:CompileSpeedPreference
set speedChoice=%args1%
if "%speedChoice%" == "" (
    echo Please define which optimization to use:
    echo   1. Size ^(same as -O1 optimization with debug info and stack trace stripped^)
    echo   2. Speed ^(same as -O2 optimization with debug info and stack trace stripped^)
    echo   3. Debug ^(no optimization^)
    echo.
    set /p speedChoice=Size^(1^)/Speed^(2^)/Debug^(3^)^> 
)

if "%speedChoice%" == "1" (
  echo Compiling with Speed preferred optimization
  set publishProfile=ReleasePublish-O1
  set configuration=Release
  goto :StartCompilation
) else if "%speedChoice%" == "2" (
  echo Compiling with Size preferred optimization
  set publishProfile=ReleasePublish-O2
  set configuration=Release
  goto :StartCompilation
) else if "%speedChoice%" == "3" (
  echo Compiling with No Optimization ^(Debug^)
  set publishProfile=DebugPublish
  set configuration=Debug
  goto :StartCompilation
)

cls
echo Input is not valid! Available choices: 1, 2 or 3
set publishProfile=
set args1=
goto :CompileSpeedPreference

:StartCompilation
set outputBaseDirPath=%projectPublishPath%\%configuration%
set outputDirPath=%outputBaseDirPath%\%_pluginName%
%~d0

:StartIndexerCompilation
if /I not exist "%indexerToolPath%" (
  mkdir "%indexerPublishPath%" || goto :Error
  cd "%indexerPath%"
  dotnet restore --runtime win-x64 ..\Hi3Helper.Plugin.HBR.sln || goto :Error
  dotnet clean --configuration Release --runtime win-x64 ..\Hi3Helper.Plugin.HBR.sln || goto :Error
  dotnet publish --configuration Release --runtime win-x64 /p:PublishProfile=ReleasePublish -o "%indexerPublishPath%" || goto :Error
)

:StartPluginCompilation
if /I exist "%projectPublishPath%" (
    rmdir /S /Q "%projectPublishPath%" || goto :Error
)
mkdir "%outputDirPath%"
cd "%projectPath%"
dotnet restore --runtime win-x64 ..\Hi3Helper.Plugin.HBR.sln || goto :Error
dotnet clean --configuration %configuration% --runtime win-x64 ..\Hi3Helper.Plugin.HBR.sln || goto :Error
dotnet publish --configuration %configuration% --runtime win-x64 /p:PublishProfile=%publishProfile% -o "%outputDirPath%" || goto :Error

:RemovePDBIfNotDebug
if not "%speedChoice%" == "3" (
  del "%outputDirPath%\*.pdb" || goto :Error
)

:StartIndexing
%indexerToolPath% %outputDirPath% || goto :Error

:StartPacking
cd %outputBaseDirPath%
%sevenzip% a -t7z -m0=lzma2 -mx=9 -aoa -mmt=%thread% -mfb=273 -md=128m -ms=on "%_pluginName%.7z" . || goto :Error
goto :CompileSuccess

:Error
echo An error has occurred while performing compilation with error status: %errorlevel%
goto :End

:CompileSuccess
echo The plugin has been compiled successfully!
echo Go to this path to see the compile output:
echo   %outputBaseDirPath%
echo.
goto :End

:End
if "%args1%" == "" (
    pause | echo Press any key to exit...
)
cd "%currentPath%"