The quick way to do this is as follows:

Initial Setup
-	Make sure there's a scanner with a working TWAIN Driver on the system, ideally TWAINDSM.DLL should be present
-	Log into Google (like with gmail)
-	If you don’t still have it, go to https://console.developers.google.com to get your app/scanner credentials
-	Copy the TWAINDirect folder to your desktop
-	Replace the  fields in TWAINDirect/source/TwainDirectApplication/appdata.txt with your credentials
-	Replace the  fields in TWAINDirect/source/TwainDirectScanner/appdata. txt with your credentials

Registration
-	Go to TWAINDirect/Windows
-	Double-click on CleanLogs.bat
-	Double-click on TwainDirectScannerRegister.bat
-	Wait to see if it prompts for your device
-	Double-click on TwainDirectApplication.bat
-	Click on Open, and Login/Accept
-	Click on the Register button
-	Copy the UUID to the TwainDirectScannerRegister (it’s in your paste buffer) and press the enter key
-	Exit from TwainDirectScannerRegister
-	Click on the OK button on the TwainDirectApplication registration dialog
-	Your scanner should appear in the list
-	If a problem occurs, check the logs in TWAINDirect/source/data/*

Scanning
-	Go to TWAINDirect/Windows
-	Double-click on TwainDirectScanner.bat, make sure that your scanner is listed
-	If not already running, double-click on TwainDirectApplication.bat
-	Click on Open, and Login/Accept
-	Select your scanner and click on the Open button
-	Use Setup to change how the session runs
-	Use Scan to scan
-	Scanning is slow, so I don’t recommend doing a lot of sheets in one batch
-	The TwainDirectScanner screen will show progress

If there’s a crash or things really get out of sync, unregister and register the scanner to clean it up, this is one of the robustness issues that’ll be addressed at some point…

sudo mozroots --import --ask-remove --machine
sudo certmgr -ssl https://talk.google.com

sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
echo "deb http://download.mono-project.com/repo/debian wheezy main" | sudo tee /etc/apt/sources.list.d/mono-xamarin.list
sudo apt-get update

echo "deb http://download.mono-project.com/repo/debian wheezy-apache24-compat main" | sudo tee -a /etc/apt/sources.list.d/mono-xamarin.list