:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Certification\bin\x64\Debug\TwainDirect.Certification.exe (
	cd "%~dp0\..\..\source\TwainDirect.Certification\data"
	start %~dp0\..\..\source\TwainDirect.Certification\bin\x64\Debug\TwainDirect.Certification.exe
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Certification\bin\x64\Release\TwainDirect.Certification.exe (
	cd "%~dp0\..\..\source\TwainDirect.Certification\data"
	start %~dp0\..\..\source\TwainDirect.Certification\bin\x64\Release\TwainDirect.Certification.exe
	exit
)
