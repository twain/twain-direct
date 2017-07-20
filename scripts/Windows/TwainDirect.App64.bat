:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.App\bin\x64\Debug\TwainDirect.App.exe (
	cd "%~dp0\..\..\source\TwainDirect.App\bin\x64\Debug"
	start TwainDirect.App.exe scale=1
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.App\bin\x64\Release\TwainDirect.App.exe (
	cd "%~dp0\..\..\source\TwainDirect.App\bin\x64\Release"
	start TwainDirect.App.exe scale=1
	exit
)
