:: Xpra Manager
:: Set development environment
:: 04/20/2020, sganis

@ECHO OFF
SET DIR=%~dp0
SET DIR=%DIR:~0,-1%


set "PATH=C:\Xpra-Client-Python3-x86_64_4.0.2-r26625;%PATH%"
::CALL %DIR%\setenv.bat
call %COMSPEC% /k CD /D %DIR%
start "" /b %DIR%\XpraManager.sln 
