:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug\TwainDirectScanner.exe (
	pushd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Debug"        
	start /b TwainDirectScanner.exe mode=terminal command=start
	popd
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirectScanner\bin\x86\Release\TwainDirectScanner.exe (
	pushd "%~dp0\..\..\source\TwainDirectScanner\bin\x86\Release"
	start /b TwainDirectScanner.exe mode=terminal command=start
	popd
)
