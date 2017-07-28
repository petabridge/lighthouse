# escape=`

FROM microsoft/windowsservercore

COPY .\src\Lighthouse\bin\Release C:\lighthouse\

RUN ["C:\\lighthouse\\Lighthouse.exe","install","--localsystem","--autostart"]