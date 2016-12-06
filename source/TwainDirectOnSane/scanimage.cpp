#include <windows.h>
#include <stdio.h>
#include <string.h>

int main(int a_iArgc, char *a_aszArgv[])
{
	// Validate...
	if (a_iArgc < 2)
	{
		return (-1);
	}

	// List devices...
	if (!strncmp(a_aszArgv[1],"-f",2))
	{
		printf
		(
			"scanner,kds_i2000:i2000,Kodak,feeder scanner,i2000,0\n"
		);
		fflush(stdout);
		return (0);
	}

	// List devices...
	if (strstr(a_aszArgv[1],"--help"))
	{
		printf
		(
			"    --resolution 200|300|600 [200]\n"
			"    --mode Lineart|Gray|Color [Gray]\n"
			"    -x 10..100mm [xx]\n"
			"    -y 10..100mm [xx]\n"
		);
		fflush(stdout);
		return (0);
	}

	// Hmmm...
	return (-1);
}
