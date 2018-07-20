# twain-direct

![](https://twaingroup.visualstudio.com/_apis/public/build/definitions/656d47c3-955a-4a3e-92c1-1c05ace55bb9/2/badge)

Main TWAIN Direct repository.

To build the project, you usually need to perform the following steps:

 - Clone TWAIN Direct repository
   ```
   git clone --recurse-submodules https://github.com/twain/twain-direct.git
   ```
 - Get latest PDF Raster sources (use this for updates only)
   ```
   git submodule update --init --recursive
   git submodule foreach git pull origin master     
   ```   
 - Build PDF Raster solution (/source/pdfraster/pdfraster.sln)
  
 - [optional] [Download nuget](https://dist.nuget.org/index.html) (if you don't have it already)
 - Install [Wix](http://wixtoolset.org/) to add installer project types in Visual Studio
 - Restore packages by running ```nuget.exe restore TwainDirect.sln```
 
 - Build PDF/raster solution (/source/pdfraster/pdfrasgter.sln)

 - Build TWAIN Direct solution (/source/TwainDirect.sln)
