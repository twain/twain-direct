Windows: Setting up for TwainDirectScanner
- install Bonjour (3xx or higher)
  To check version, run "dns-sd -V" from command window:
    C:\WINDOWS\system32>dns-sd -V
    Currently running daemon (system service) is version 333.18
  Newer version on TWAIN's Google Drive at
    SWORD/Documents/TWAIN Direct/TWAIN Local/Tools/_README.txt
- copy the TwainDirect folder to the desktop
- run CreateCertificate.bat as admin, follow the directions
- run AddShowRemoveFirewall.bat as admin, and select the "add" option
- run AddShowRemoveHttpUrlacl.bat as admin, and select the "adds" option
- run TwainDirectScannerRegister.bat, to pick a TWAIN driver
- run TwainDirectScannerStart.bat, to start monitoring for TWAIN Local connections
- optional debug step: run dns-sd -B _privet._tcp to see bonjour connections
- run TwainDirectApplication.bat, to talk to the scanner

Linux/macOS: Setting up for TwainDirectScanner
- copy the TwainDirect folder to the desktop
- bring up a terminal window and change to the TwainDirect\LinuxMac folder
- run TwainDirectScannerRegister.sh, to pick a TWAIN driver
- run TwainDirectScannerStart.sh, to start monitoring for TWAIN Local connections
- run TwainDirectScannerApplication.sh, to talk to the scanner
