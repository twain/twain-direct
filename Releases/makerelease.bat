@echo off
:: 
:: makerelease v1.0 24-Mar-2020
:: Ask some questions, and copy the built files into this folder
:: to make releasing stuff easier and less error prone.
::

::
:: Show the version info, and get an okay on it...
::
echo.
echo | set /p dummyName="TwainDirect.App........................."
findstr /C:"AssemblyFileVersion" "%~dp0%..\source\TwainDirect.App\Properties\AssemblyInfo.cs"
echo | set /p dummyName="TwainDirect.App.Installer..............."
findstr /C:"define MajorVersion" "%~dp0%..\source\TwainDirect.App.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.App.Installer..............."
findstr /C:"define MinorVersion" "%~dp0%..\source\TwainDirect.App.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.App.Installer..............."
findstr /C:"define BuildVersion" "%~dp0%..\source\TwainDirect.App.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.App.Installer..............."
findstr /C:"define Revision" "%~dp0%..\source\TwainDirect.App.Installer\Details.wxi"
::
echo.
echo | set /p dummyName="TwainDirect.Certification..............."
findstr /C:"AssemblyFileVersion" "%~dp0%..\source\TwainDirect.Certification\Properties\AssemblyInfo.cs"
echo | set /p dummyName="TwainDirect.Certification.Installer....."
findstr /C:"define MajorVersion" "%~dp0%..\source\TwainDirect.Certification.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Certification.Installer....."
findstr /C:"define MinorVersion" "%~dp0%..\source\TwainDirect.Certification.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Certification.Installer....."
findstr /C:"define BuildVersion" "%~dp0%..\source\TwainDirect.Certification.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Certification.Installer....."
findstr /C:"define Revision" "%~dp0%..\source\TwainDirect.Certification.Installer\Details.wxi"
::
echo.
echo | set /p dummyName="TwainDirect.OnTwain....................."
findstr /C:"AssemblyFileVersion" "%~dp0%..\source\TwainDirect.OnTwain\Properties\AssemblyInfo.cs"
::
echo.
echo | set /p dummyName="TwainDirect.Scanner....................."
findstr /C:"AssemblyFileVersion" "%~dp0%..\source\TwainDirect.Scanner\Properties\AssemblyInfo.cs"
echo | set /p dummyName="TwainDirect.Scanner.Installer..........."
findstr /C:"define MajorVersion" "%~dp0%..\source\TwainDirect.Scanner.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Scanner.Installer..........."
findstr /C:"define MinorVersion" "%~dp0%..\source\TwainDirect.Scanner.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Scanner.Installer..........."
findstr /C:"define BuildVersion" "%~dp0%..\source\TwainDirect.Scanner.Installer\Details.wxi"
echo | set /p dummyName="TwainDirect.Scanner.Installer..........."
findstr /C:"define Revision" "%~dp0%..\source\TwainDirect.Scanner.Installer\Details.wxi"
::
echo.
set answer=
set /p answer="Are you happy with the version info (Y/n)? "
if "%answer%" == "" goto VERSIONDONE
if "%answer%" == "y" goto VERSIONDONE
goto:EOF
::
:VERSIONDONE


::
:: Delete the current folder, and recreate it with subfolders...
::
echo.
echo *** Cleaning the twaindirect_00000000 folder...
rmdir /s /q "%~dp0%twaindirect_00000000" > NUL 2>&1
mkdir "%~dp0%twaindirect_00000000"


::
:: Copy the files...
::
echo.
echo *** Copying files to the twaindirect_00000000 folder...
xcopy "%~dp0%..\source\TWAIN Direct.rtf" "twaindirect_00000000\" | find /V "File(s)"
xcopy "%~dp0%..\source\TwainDirect.OnTwain\TWAIN CS.rtf" "twaindirect_00000000\" | find /V "File(s)"
xcopy "%~dp0%..\source\TwainDirect.App.Installer\bin\Release\en-US\*.msi" "twaindirect_00000000\" | find /V "File(s)"
xcopy "%~dp0%..\source\TwainDirect.Certification.Installer\bin\Release\en-US\*.msi" "twaindirect_00000000\" | find /V "File(s)"
xcopy "%~dp0%..\source\TwainDirect.Scanner.Installer\bin\Release\en-US\*.msi" "twaindirect_00000000\" | find /V "File(s)"


::
:: All done...
::
echo.
echo *** All done, be sure to rename the twaindirect_00000000 folder before committing...
goto:EOF
