#!/bin/bash

# Home...
SOURCE=`dirname ${BASH_SOURCE[0]}`

# 
# Debug...
#
if [ -f "${SOURCE}/../source/TwainDirectScanner/bin/x86/Debug/TwainDirectScanner.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectScanner/bin/x86/Debug"
	mono TwainDirectScanner.exe
	exit
fi

# 
# Release...
#
if [ -f "${SOURCE}/../source/TwainDirectScanner/bin/x86/Release/TwainDirectScanner.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectScanner/bin/x86/Release"
	mono TwainDirectScanner.exe
	exit
fi
