@echo off
echo AddShowRemoveHttpUrlacl.bat v1.1 15-Nov-2016
echo Copyright (c) 2016 Kodak Alaris Inc.
echo.


::
:: Test for admin rights...
::
echo ***Test for admin rights...
net session >nul: 2>&1
if %errorLevel% NEQ 0 (
    echo Please run this batch script as admin...
    pause >nul:
    goto:eof
)


::
:: Prompt if there's no argument...
::
set command=%1
if "%1" == "" (
	set /p command="add, adds, show, or remove? "
)


::
:: Handle the command
::
if "%command%" == "add" (
	netsh http add urlacl "url=http://+:55555/privet/info/" "sddl=D:(A;;GX;;;BU)"  
	netsh http add urlacl "url=http://+:55555/privet/twaindirect/session/" "sddl=D:(A;;GX;;;BU)"
) else (
	if "%command%" == "adds" (
		netsh http add urlacl "url=https://+:55555/privet/info/" "sddl=D:(A;;GX;;;BU)"
		netsh http add urlacl "url=https://+:55555/privet/twaindirect/session/" "sddl=D:(A;;GX;;;BU)"
		netsh http add sslcert ipport=0.0.0.0:55555 certhash=873803d4c42c2359fd985c1c1f833d60bd5db154 appid={aadc29dd-1d81-42f5-873d-5d89cf6e58ee} certstore=my
	) else (
		if "%command%" == "show" (
			netsh http show urlacl "url=http://+:55555/privet/info/"
			netsh http show urlacl "url=https://+:55555/privet/info/"
			netsh http show urlacl "url=http://+:55555/privet/twaindirect/session/"
			netsh http show urlacl "url=https://+:55555/privet/twaindirect/session/"
			netsh http show sslcert ipport=0.0.0.0:55555
		) else (
			if "%command%" == "remove" (
				netsh http delete urlacl "url=http://+:55555/privet/info/"
				netsh http delete urlacl "url=https://+:55555/privet/info/"
				netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/"
				netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/"
				netsh http delete sslcert ipport=0.0.0.0:55555
			) else (
				echo Please specify "add", "adds", "show", or "remove"
			)
		)
	)
)


::
:: All done...
::
pause
goto:eof
