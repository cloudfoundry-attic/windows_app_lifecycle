:: msbuild must be in path
SET PATH=%PATH%;%WINDIR%\Microsoft.NET\Framework64\v4.0.30319;%WINDIR%\SysNative
where msbuild
if errorLevel 1 ( echo "msbuild was not found on PATH" && exit /b 1 )

:: enable some features
dism /online /Enable-Feature /FeatureName:IIS-WebServer /All /NoRestart
dism /online /Enable-Feature /FeatureName:IIS-WebSockets /All /NoRestart
dism /online /Enable-Feature /FeatureName:Application-Server-WebServer-Support /FeatureName:AS-NET-Framework /All /NoRestart
dism /online /Enable-Feature /FeatureName:IIS-HostableWebCore /All /NoRestart

rmdir /S /Q output
rmdir /S /Q packages
bin\nuget restore || exit /b 1

MSBuild WindowsAppLifecycle.sln /t:Rebuild /p:Configuration=Release || exit /b 1
xcopy bin\bsdtar.exe Builder\bin
move /Y Builder\bin\bsdtar.exe Builder\bin\tar.exe
bin\bsdtar -czvf windows_app_lifecycle.tgz --exclude log -C Builder\bin . -C ..\..\Launcher\bin . -C ..\..\WebAppServer\bin . || exit /b 1
for /f "tokens=*" %%a in ('git rev-parse --short HEAD') do (
    set VAR=%%a
)

mkdir output
move /Y windows_app_lifecycle.tgz output\windows_app_lifecycle-%VAR%.tgz || exit /b 1
