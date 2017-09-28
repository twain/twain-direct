@echo off
echo AddShowRemoveHttpUrlacl.bat v1.2 23-Mar-2017
echo Copyright (c) 2016 - 2017 Kodak Alaris Inc.
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
	set /p command="addhttps, addhttp, show, or remove? "
)


::
:: Dispatcher...
::
if "%command%" == "addhttp" goto:Addhttp
if "%command%" == "addhttps" goto:Addhttps
if "%command%" == "show" goto:Show
if "%command%" == "remove" goto:Remove
echo Please specify "add", "adds", "show", or "remove"
goto:done


::
:: HTTP
::
:Addhttp
	echo Setting up for HTTP access (not recommended)
        netsh http delete urlacl "url=http://+:34034/twaindirect/v1/commands/" > NUL
        netsh http delete urlacl "url=https://+:34034/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/infoex/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/infoex/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/twaindirect/session/" > NUL
       	netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
       	netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete sslcert ipport=0.0.0.0:55555 > NUL
	netsh http delete sslcert ipport=0.0.0.0:34034 > NUL
	netsh http add urlacl "url=http://+:34034/privet/info/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add urlacl "url=http://+:34034/privet/infoex/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add urlacl "url=http://+:34034/privet/twaindirect/session/" "sddl=D:(A;;GX;;;S-1-2-0)"
	goto:done

:Addhttps
	echo Setting up for HTTPS access
       	netsh http delete urlacl "url=http://+:34034/twaindirect/v1/commands/" > NUL
       	netsh http delete urlacl "url=https://+:34034/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/infoex/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/infoex/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:34034/privet/twaindirect/session/" > NUL
       	netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
       	netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete sslcert ipport=0.0.0.0:55555 > NUL
	netsh http delete sslcert ipport=0.0.0.0:34034 > NUL
	netsh http add urlacl "url=https://+:34034/privet/info/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add urlacl "url=https://+:34034/privet/infoex/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add urlacl "url=https://+:34034/privet/twaindirect/session/" "sddl=D:(A;;GX;;;S-1-2-0)"
	netsh http add sslcert ipport=0.0.0.0:34034 certhash=74c85ff382477bfbb5f63faca8117a913dc01c44 appid={aadc29dd-1d81-42f5-873d-5d89cf6e58ee} certstore=my
	goto:done

:Show	
	echo showing
	netsh http show urlacl "url=http://+:34034/privet/info/"
	netsh http show urlacl "url=https://+:34034/privet/info/"
	netsh http show urlacl "url=http://+:34034/privet/infoex/"
	netsh http show urlacl "url=https://+:34034/privet/infoex/"
	netsh http show urlacl "url=http://+:34034/privet/twaindirect/session/"
	netsh http show urlacl "url=https://+:34034/privet/twaindirect/session/"
	netsh http show sslcert ipport=0.0.0.0:34034
	goto:done

:Remove
	echo removing
	netsh http delete urlacl "url=http://+:34034/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=https://+:34034/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:34034/privet/info/"
	netsh http delete urlacl "url=https://+:34034/privet/info/"
	netsh http delete urlacl "url=http://+:34034/privet/infoex/"
	netsh http delete urlacl "url=https://+:34034/privet/infoex/"
	netsh http delete urlacl "url=http://+:34034/privet/twaindirect/session/"
	netsh http delete urlacl "url=https://+:34034/privet/twaindirect/session/"
       	netsh http delete urlacl "url=http://+:55555/twaindirect/v1/commands/" > NUL
       	netsh http delete urlacl "url=https://+:55555/twaindirect/v1/commands/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/info/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/infoex/" > NUL
	netsh http delete urlacl "url=http://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete urlacl "url=https://+:55555/privet/twaindirect/session/" > NUL
	netsh http delete sslcert ipport=0.0.0.0:55555
	netsh http delete sslcert ipport=0.0.0.0:34034
	goto:done

::
:: All done...
::
:done
pause
goto:eof
