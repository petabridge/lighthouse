# escape=`

FROM microsoft/windowsservercore

COPY .\src\Lighthouse\bin\Release C:\lighthouse\
COPY .\src\Lighthouse.WhatsMyIP\bin\Release C:\lighthouse.whatsmyip\

RUN ["C:\\lighthouse\\Lighthouse.exe","install","--localsystem","--autostart"]
RUN ["C:\\lighthouse.whatsmyip\\Lighthouse.WhatsMyIP.exe","install","--localsystem","--autostart"]