:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug\TwainDirectScanner.exe (
	cd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug"
	start TwainDirectScanner.exe
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Release\TwainDirectScanner.exe (
	cd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Release"
	start TwainDirectScanner.exe
	exit
)
