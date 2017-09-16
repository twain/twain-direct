@echo off
::
:: Built from the instructions at:
:: https://blogs.technet.microsoft.com/jhoward/2005/02/02/how-to-use-makecert-for-trusted-root-certification-authority-and-ssl-certificate-issuance/
::


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
:: Validate...
::
call "C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\Tools\VsDevCmd.bat"
echo ***Test that we're running in a Visual Studio shell...
if "%VSINSTALLDIR%" == "" (
    echo Please run this batch script from a Visual Studio shell...
    pause
    goto:eof
)


::
:: Create a new root certificate...
::
:: -pe					Exportable private key
:: -n "CN=Test And Dev Root Authority"	Subject name
:: -ss my				Certificate store name
:: -sr LocalMachine			Certificate store location
:: -a sha1				Signature algorithm
:: -sky signature			Subject key type is for signature purposes
:: -r					Make a self-signed cert
:: "Test And Dev Root Authority.cer"	Output filename
::
echo Creating a new certificate...
makecert -pe^
 	 -n "CN=TWAIN Direct Root Authority for %COMPUTERNAME%"^
 	 -ss my^
 	 -sr LocalMachine^
 	 -a sha1^
 	 -sky signature^
 	 -r^
 	 "TWAIN Direct Root Authority for %COMPUTERNAME%.cer"


::
:: Next steps are manual...
::
echo.
echo Please do the following steps if you want to confirm that
echo the certificate has been created and is in your personal
echo node...
echo 1 - Start/Run/MMC.EXE
echo 2 - File/Add-Remove Snap-In
echo 3 - Click Add
echo 4 - Select Certificates and click Add
echo 5 - Select Computer Account and hit Next
echo 6 - Select Local Computer
echo 7 - Click Close
echo 8 - Click OK
pause press ENTER when ready to continue...


::
:: Make our exchange certificate using the root certificate we made up above...
::
:: -pe							Exportable private key
:: -n "CN=jhoward-5160"					Full DNS name of the target machine. Note that in this example,
::							I am running a machine with the NetBIOS name "jhoward-5160"
::							which is not a member of a domain. Hence, the full DNS name
::							really is this. Replace this as appropriate. e.g. CN=mycomputer.company.com
:: -ss my						Certificate store name
:: -sr LocalMachine					Certificate store location
:: -a sha1						Signature algorithm
:: -sky exchange					Subject key type is for key-exchange purposes (i.e. Encryption)
:: -eku 1.3.6.1.5.5.7.3.1				Enhanced key usage OIDs. Trust me on this :)
:: -in "TWAIN Direct Root Authority for %COMPUTERNAME%"	Issuers certificate common name
:: -is my						Issuers certificate store name
:: -ir LocalMachine					Issuers certificate store location
:: -sp "Microsoft RSA SChannel Cryptographic Provider"	CryptoAPI providers name
:: -sy 12						CryptoAPI providers type
:: jhoward-5160.cer					Output file – replace and name as appropriate.
::
::
::
::
makecert -pe^
 	 -n "CN=%COMPUTERNAME%.local"^
 	 -ss my^
 	 -sr LocalMachine^
 	 -a sha1^
 	 -sky exchange^
 	 -eku 1.3.6.1.5.5.7.3.1^
 	 -in "TWAIN Direct Root Authority for %COMPUTERNAME%"^
 	 -is my^
 	 -ir LocalMachine^
 	 -sp "Microsoft RSA SChannel Cryptographic Provider"^
 	 -sy 12^
 	 "TWAIN Direct Exchange for %COMPUTERNAME%.cer"


::
:: More manual stuff...
::
echo.
echo Go back to the certificates snap-in, right-click the
echo "TWAIN Direct Root Authority for %COMPUTERNAME%" certificate and copy it
echo to the "Trusted Root Certification Authorities" node.
echo Once done, if you expand this node, and then select
echo certificates your newly created root cert should be
echo present.  You can copy it by dragging it to the desired
echo node...
pause press ENTER when ready to continue...


::
:: Last steps...
::
echo.
echo Go to your personal node and double-click on the
echo "%COMPUTERNAME%.local" certificate issued by "Test and Dev
echo Root Authority".  Click on the Details tab and scroll
echo down to the Thumbprint.  Click on Thumbprint.  Hex
echo values will appear in the window.  Copy this to the
echo certhash= argument in the AddShowRemoveHttpUrlacl.bat
echo file. Remove the spaces and save the file.  If the
echo editor complains that Unicode data is present, open
echo the file back up and look for a question mark in the
echo certhash= data, remove it and save the file again...
pause press ENTER when ready to continue...
