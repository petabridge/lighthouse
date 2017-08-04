@ECHO OFF
REM The following directory is for .NET 4.0
sc stop Lighthouse >> log.txt 2>> err.txt
echo Uninstalling WindowsService...
echo ---------------------------------------------------
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u ..\Lighthouse.Service.exe >> log.txt 2>> err.txt
echo ---------------------------------------------------
echo Done