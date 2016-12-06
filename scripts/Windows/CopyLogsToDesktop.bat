:: 
:: Copy the logs to the desktop...
::
set TDL=%HOMEPATH%\Desktop\TwainDirectLogs
rmdir /q /s %TDL% >NUL:
mkdir %TDL%
copy %~dp0\..\..\source\data\TwainDirectApplication\*.Log %TDL%\
copy %~dp0\..\..\source\data\TwainDirectOnTwain\*.Log %TDL%\
copy %~dp0\..\..\source\data\TwainDirectScanner\*.Log %TDL%\
