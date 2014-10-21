@ECHO OFF
echo Installing WindowsService...
echo ---------------------------------------------------
REM pass additional arguments here
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /i ..\Lighthouse.Service.exe >> log.txt 2>> err.txt
echo ---------------------------------------------------
echo Done
sc start Lighthouse >> log.txt 2>> err.txt