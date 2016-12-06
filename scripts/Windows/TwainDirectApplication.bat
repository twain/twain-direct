:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirectApplication\bin\x86\Debug\TwainDirectApplication.exe (
	cd "%~dp0\..\..\source\TwainDirectApplication\bin\x86\Debug"
	start TwainDirectApplication.exe scale=1
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirectApplication\bin\x86\Release\TwainDirectApplication.exe (
	cd "%~dp0\..\..\source\TwainDirectApplication\bin\x86\Release"
	start TwainDirectApplication.exe scale=1
	exit
)
