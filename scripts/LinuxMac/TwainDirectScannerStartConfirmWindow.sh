#!/bin/bash

# Home...
SOURCE=`dirname ${BASH_SOURCE[0]}`

# Machine...
if [ "`uname -m`" == "x86_64" ]; then
	MACHINE=x64
else
	MACHINE=x86
fi

# 
# Debug...
#
if [ -f "${SOURCE}/../source/TwainDirectScanner/bin/${MACHINE}/Debug/TwainDirectScanner.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectScanner/bin/${MACHINE}/Debug"
	mono TwainDirectScanner.exe mode=window command=start confirmscan
	exit
fi

# 
# Release...
#
if [ -f "${SOURCE}/../source/TwainDirectScanner/bin/${MACHINE}/Release/TwainDirectScanner.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectScanner/bin/${MACHINE}/Release"
	mono TwainDirectScanner.exe mode=window command=start confirmscan
	exit
fi
