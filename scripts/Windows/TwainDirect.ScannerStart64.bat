:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Debug\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Debug"
	start /b TwainDirect.Scanner.exe mode=terminal command=start
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Release\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Release"
	start /b TwainDirect.Scanner.exe mode=terminal command=start
	exit
)
