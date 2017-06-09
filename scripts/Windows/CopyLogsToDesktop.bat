:: 
:: Copy the logs to the desktop...
::
set TDL=%HOMEDRIVE%%HOMEPATH%\Desktop\TwainDirectLogs
rmdir /q /s %TDL% >NUL:
mkdir %TDL%
copy %appdata%\twaindirect\TwainDirect.App\*.log %TDL%\
copy %appdata%\twaindirect\TwainDirect.Certification\*.log %TDL%\
copy %appdata%\twaindirect\TwainDirect.OnTwain\*.log %TDL%\
copy %appdata%\twaindirect\TwainDirect.Scanner\*.log %TDL%\
