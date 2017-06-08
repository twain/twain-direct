# twain-direct

![](https://twaingroup.visualstudio.com/_apis/public/build/definitions/656d47c3-955a-4a3e-92c1-1c05ace55bb9/2/badge)

Main TWAIN Direct repository.

To build the project, you usually need to perform the following steps:

 - Clone TWAIN Direct repository
   ```
   git clone https://github.com/twain/twain-direct.git
   ```
 - Get latest PDF Raster sources
   ```
   git submodule update --init --recursive
   git submodule foreach git pull origin master     
   ```   
 - Build PDF Raster solution (/source/pdfraster/pdfraster.sln)
 - Build TWAIN Direct solution (/source/TwainDirect.sln)
