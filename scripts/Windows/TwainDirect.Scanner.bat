:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug"
	start TwainDirect.Scanner.exe
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release"
	start TwainDirect.Scanner.exe
	exit
)
