:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug"
	start /b TwainDirect.Scanner.exe mode=terminal command=start confirmscan
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release"
	start /b TwainDirect.Scanner.exe mode=terminal command=start confirmscan
	exit
)
