:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Certification\bin\x86\Debug\TwainDirect.Certification.exe (
	cd "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Debug"
	start %~dp0\..\..\source\TwainDirect.Certification\bin\x86\Debug\TwainDirect.Certification.exe
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Certification\bin\x86\Release\TwainDirect.Certification.exe (
	cd "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Release"
	start %~dp0\..\..\source\TwainDirect.Certification\bin\x86\Release\TwainDirect.Certification.exe
	exit
)
