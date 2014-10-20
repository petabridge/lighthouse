REM Skip this startup task if we're running in the emulator
if "%EMULATED%"=="true" exit /b 0

@ECHO OFF
echo Installing WindowsService...
echo ---------------------------------------------------
REM pass additional arguments here
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /i Lighthouse.Service.exe 127.0.0.1 4444 >> log.txt 2>> err.txt
sc start Lighthouse.Service >> log.txt 2>> err.txt
echo ---------------------------------------------------
echo Done

REM %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /i Lighthouse.Service.exe