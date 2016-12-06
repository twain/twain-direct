@echo off
echo AddShowRemoveFirewall.bat v1.0 03-Nov-2016
echo Copyright (c) 2016 Kodak Alaris Inc.
echo.


::
:: Test for admin rights...
::
echo ***Test for admin rights...
net session >nul: 2>&1
if %errorLevel% NEQ 0 (
    echo Please run this batch script as admin...
    pause >nul:
    goto:eof
)


::
:: Prompt if there's no argument...
::
set command=%1
if "%1" == "" (
	set /p command="add, show, or remove? "
)


::
:: Handle the command
::
if "%command%" == "add" (
	call:AddToFirewall "TwainDirectApplication"		"out"	"%~dp0\..\source\TwainDirectApplication\bin\x86"		"TwainDirectApplication.exe"
	call:AddToFirewall "TwainDirectApplicationDebug"	"out"	"%~dp0\..\source\TwainDirectApplication\bin\x86\Debug"		"TwainDirectApplication.exe"
	call:AddToFirewall "TwainDirectScanner"			"in"	""								"system"
	call:AddToFirewall "TwainDirectScannerDebug"		"in"	""								"system"
) else (
	if "%command%" == "show" (
		call:ShowFirewall "TwainDirectApplication"
		call:ShowFirewall "TwainDirectApplicationDebug"
		call:ShowFirewall "TwainDirectScanner"
		call:ShowFirewall "TwainDirectScannerDebug"
	) else (
		if "%command%" == "remove" (
			call:RemoveFromFirewall "TwainDirectApplication"
			call:RemoveFromFirewall "TwainDirectApplicationDebug"
			call:RemoveFromFirewall "TwainDirectScanner"
			call:RemoveFromFirewall "TwainDirectScannerDebug"
		) else (
			echo Please specify "add", "show", or "remove"
		)
	)
)


::
:: All done...
::
pause
goto:eof


REM https://support.microsoft.com/en-us/kb/947709


::
:: Function: add to the firewall, try to be as restrictive as possible, if
:: the third argument is empty, then we want to use the fourth argument
:: without any changes.
::
:AddToFirewall
	if "%~3" == "" (
		if "%~2" == "in" (
			netsh advfirewall firewall add rule "name=%~1" "dir=%~2" "action=allow" "program=%~4" "enable=yes" profile=any interfacetype=any protocol=tcp localport=55555 remoteport=any security=notrequired localip=any remoteip=localsubnet edge=yes
		) else (
			netsh advfirewall firewall add rule "name=%~1" "dir=%~2" "action=allow" "program=%~4" "enable=yes" profile=any interfacetype=any protocol=tcp localport=55555 remoteport=any security=notrequired localip=any remoteip=localsubnet
		)
	) else (
		pushd "%~3"
		if "%~2" == "in" (
			netsh advfirewall firewall add rule "name=%~1" "dir=%~2" "action=allow" "program=%cd%\%~4" "enable=yes" profile=any interfacetype=any protocol=tcp localport=55555 remoteport=any security=notrequired localip=any remoteip=localsubnet edge=yes
		) else (
			netsh advfirewall firewall add rule "name=%~1" "dir=%~2" "action=allow" "program=%cd%\%~4" "enable=yes" profile=any interfacetype=any protocol=tcp localport=55555 remoteport=any security=notrequired localip=any remoteip=localsubnet
		)
		popd
	)
goto:eof


::
:: Function: show the firewall
::
:ShowFirewall
	netsh advfirewall firewall show rule "name=%~1"
goto:eof


::
:: Function: remove from the firewall
::
:RemoveFromFirewall
	netsh advfirewall firewall delete rule "name=%~1"
goto:eof
