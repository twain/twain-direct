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
	:: clear old stuff first
	echo Setting up for HTTP access
        netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
        netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete sslcert ipport=0.0.0.0:55555 > NUL
	:: now add the new stuff
	netsh http add urlacl "url=http://+:55555/privet/info/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add urlacl "url=http://+:55555/privet/twaindirect/session/" "sddl=D:(A;;GX;;;S-1-2-0)"
) else (
	if "%command%" == "adds" (
		echo Setting up for HTTPS access
		:: clear old stuff first
        	netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
        	netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
		netsh http delete urlacl "url=http://+:55555/privet/info/" > NUL
		netsh http delete urlacl "url=https://+:55555/privet/info/" > NUL
		netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/" > NUL
		netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/" > NUL
		netsh http delete sslcert ipport=0.0.0.0:55555 > NUL
		:: now add the new stuff
		netsh http add urlacl "url=https://+:55555/privet/info/" "sddl=D:(A;;GX;;;S-1-2-0)"
		netsh http add urlacl "url=https://+:55555/privet/twaindirect/session/" "sddl=D:(A;;GX;;;S-1-2-0)"
		netsh http add sslcert ipport=0.0.0.0:55555 certhash=4bcc20ab03c592d4db80ad2b4cce893f9c54ab30 appid={aadc29dd-1d81-42f5-873d-5d89cf6e58ee} certstore=my
	) else (
		if "%command%" == "show" (
			netsh http show urlacl "url=http://+:55555/privet/info/"
			netsh http show urlacl "url=https://+:55555/privet/info/"
			netsh http show urlacl "url=http://+:55555/privet/twaindirect/session/"
			netsh http show urlacl "url=https://+:55555/privet/twaindirect/session/"
			netsh http show sslcert ipport=0.0.0.0:55555
		) else (
			if "%command%" == "remove" (
        			netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
        			netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
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
