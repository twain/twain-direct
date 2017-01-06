Windows: Setting up for TwainDirectScanner
- install Bonjour (3.x or higher)
- copy the TwainDirect folder to the desktop
- run CreateCertificate.bat as admin, follow the directions
- run AddShowRemoveFirewall.bat as admin, and select the "add" option
- run AddShowRemoveHttpUrlacl.bat as admin, and select the "adds" option
- run TwainDirectScannerRegister.bat, to pick a TWAIN driver
- run TwainDirectScannerStart.bat, to start monitoring for TWAIN Local connections
- run TwainDirectScannerApplication.bat, to talk to the scanner

Linux/macOS: Setting up for TwainDirectScanner
- copy the TwainDirect folder to the desktop
- bring up a terminal window and change to the TwainDirect\LinuxMac folder
- run TwainDirectScannerRegister.sh, to pick a TWAIN driver
- run TwainDirectScannerStart.sh, to start monitoring for TWAIN Local connections
- run TwainDirectScannerApplication.sh, to talk to the scanner
