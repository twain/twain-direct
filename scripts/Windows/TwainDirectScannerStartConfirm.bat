:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug\TwainDirectScanner.exe (
	cd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug"
	start /b TwainDirectScanner.exe mode=terminal command=start confirmscan
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Release\TwainDirectScanner.exe (
	cd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Release"
	start /b TwainDirectScanner.exe mode=terminal command=start confirmscan
	exit
)
