::
:: Test for admin rights...
::
echo ***Test for admin rights...
net session >nul: 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Please run this batch script as admin...
    pause
    goto:eof
)

::
:: Pick an NGEN folder...
::
if exist "%windir%\Microsoft.NET\Framework64\v4.0.30319" (
	set ngen64=%windir%\Microsoft.NET\Framework64\v4.0.30319\ngen
) else (
	if exist "%windir%\Microsoft.NET\Framework64\v2.0.50727" (
		set ngen64=%windir%\Microsoft.NET\Framework64\v2.0.50727\ngen
	)
)
if exist "%windir%\Microsoft.NET\Framework\v4.0.30319" (
	set ngen32=%windir%\Microsoft.NET\Framework\v4.0.30319\ngen
) else (
	if exist "%windir%\Microsoft.NET\Framework\v2.0.50727" (
		set ngen32=%windir%\Microsoft.NET\Framework\v2.0.50727\ngen
	)
)

::
:: TwainDirect.App
::
if exist "%~dp0\..\..\source\TwainDirect.App\bin\x86\Debug\TwainDirect.App.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.App\bin\x86\Debug\TwainDirect.App.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.App\bin\x86\Release\TwainDirect.App.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.App\bin\x86\Release\TwainDirect.App.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.App\bin\x64\Debug\TwainDirect.App.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.App\bin\x64\Debug\TwainDirect.App.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.App\bin\x64\Release\TwainDirect.App.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.App\bin\x64\Release\TwainDirect.App.exe"
)

::
:: TwainDirect.Certification
::
if exist "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Debug\TwainDirect.Certification.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Debug\TwainDirect.Certification.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Release\TwainDirect.Certification.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.Certification\bin\x86\Release\TwainDirect.Certification.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Certification\bin\x64\Debug\TwainDirect.Certification.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.Certification\bin\x64\Debug\TwainDirect.Certification.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Certification\bin\x64\Release\TwainDirect.Certification.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.Certification\bin\x64\Release\TwainDirect.Certification.exe"
)

::
:: TwainDirect.OnTwain
::
if exist "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x86\Debug\TwainDirect.OnTwain.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x86\Debug\TwainDirect.OnTwain.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x86\Release\TwainDirect.OnTwain.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x86\Release\TwainDirect.OnTwain.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x64\Debug\TwainDirect.OnTwain.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x64\Debug\TwainDirect.OnTwain.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x64\Release\TwainDirect.OnTwain.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.OnTwain\bin\x64\Release\TwainDirect.OnTwain.exe"
)

::
:: TwainDirect.Scanner
::
if exist "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug\TwainDirect.Scanner.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug\TwainDirect.Scanner.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release\TwainDirect.Scanner.exe" (
	%ngen32% install "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release\TwainDirect.Scanner.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Debug\TwainDirect.Scanner.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Debug\TwainDirect.Scanner.exe"
)
if exist "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Release\TwainDirect.Scanner.exe" (
	%ngen64% install "%~dp0\..\..\source\TwainDirect.Scanner\bin\x64\Release\TwainDirect.Scanner.exe"
)

pause
