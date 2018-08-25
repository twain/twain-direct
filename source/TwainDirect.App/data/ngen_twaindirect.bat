@echo off
echo ngen_twaindirect.bat v1.0 25-Aug-2018
echo Copyright (c) 2018 Kodak Alaris Inc.
echo.
set LOGFILE=%~dp0%\log.txt
echo. >> "%LOGFILE%" 2>&1
echo ########################################## >> "%LOGFILE%" 2>&1
echo ngen_twaindirect.bat program >> "%LOGFILE%" 2>&1
echo %DATE% %TIME% >> "%LOGFILE%" 2>&1


::
:: Guard against shell weirdness...
::
if "%PROCESSOR_ARCHITEW6432%" neq "" (
	echo This script must be run from a native command shell.  Please
	echo run %windir%\system32\cmd.exe as administrator, and run this
	echo script from that shell...
	pause
	goto:eof
)


::
:: Test for admin rights...
::
net session >nul: 2>&1
if %errorLevel% neq 0 (
	echo Please run this batch script as admin...
	pause
	goto:eof
)


::
:: Pick an architecture...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	if exist "%windir%\Microsoft.NET\Framework\v4.0.30319" (
		set NGENEXE=%windir%\Microsoft.NET\Framework\v4.0.30319\ngen.exe
	) else (
		if exist "%windir%\Microsoft.NET\Framework\v2.0.50727" (
			set NGENEXE=%windir%\Microsoft.NET\Framework\v2.0.50727\ngen.exe
		)
		else
		(
			echo Sorry, cannot find an NGEN to run...
			pause
			goto:eof
		)
	)
) else (
	if exist "%windir%\Microsoft.NET\Framework64\v4.0.30319" (
		set NGENEXE=%windir%\Microsoft.NET\Framework64\v4.0.30319\ngen.exe
	) else (
		if exist "%windir%\Microsoft.NET\Framework64\v2.0.50727" (
			set NGENEXE=%windir%\Microsoft.NET\Framework64\v2.0.50727\ngen.exe
		)
		else
		(
			echo Sorry, cannot find an NGEN to run...
			pause
			goto:eof
		)
	)
)
echo "NGENEXE is %NGENEXE%" >> "%LOGFILE%" 2>&1


::
:: NGEN TWAIN Direct Application...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	cd /d "%ProgramFiles%\TWAIN Direct Application"
	echo "%ProgramFiles%\TWAIN Direct Application\TwainDirect.App.exe"
	echo "%ProgramFiles%\TWAIN Direct Application\TwainDirect.App.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles%\TWAIN Direct Application\TwainDirect.App.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
) else (
	cd /d "%ProgramFiles(x86)%\TWAIN Direct Application"
	echo "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.App.exe"
	echo "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.App.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.App.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
)


::
:: NGEN TWAIN Direct TwainLocalManager (Application)...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	cd /d "%ProgramFiles%\TWAIN Direct Application"
	echo "%ProgramFiles%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe"
	echo "%ProgramFiles%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
) else (
	cd /d "%ProgramFiles(x86)%\TWAIN Direct Application"
	echo "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe"
	echo "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles(x86)%\TWAIN Direct Application\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
)


::
:: NGEN TWAIN Direct Scanner...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	cd /d "%ProgramFiles%\TWAIN Direct Scanner"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.exe"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
) else (
	cd /d "%ProgramFiles(x86)%\TWAIN Direct Scanner"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.exe"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
)


::
:: NGEN TWAIN Direct OnTwain...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	cd /d "%ProgramFiles%\TWAIN Direct Scanner"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
) else (
	cd /d "%ProgramFiles(x86)%\TWAIN Direct Scanner"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.OnTwain.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
)


::
:: NGEN TWAIN Direct TwainLocalManager (Scanner)...
::
if "%PROCESSOR_ARCHITECTURE%" == "x86" (
	cd /d "%ProgramFiles%\TWAIN Direct Scanner"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe"
	echo "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
) else (
	cd /d "%ProgramFiles(x86)%\TWAIN Direct Scanner"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe"
	echo "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	"%NGENEXE%" install "%ProgramFiles(x86)%\TWAIN Direct Scanner\TwainDirect.Scanner.TwainLocalManager.exe" >> "%LOGFILE%" 2>&1
	timeout /t 3 /nobreak > NUL 2>&1
)


::
:: All done...
::
echo.
echo We're all done...
echo Done >> "%LOGFILE%" 2>&1
pause