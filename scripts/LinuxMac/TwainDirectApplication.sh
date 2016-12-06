#!/bin/bash

# Home...
SOURCE=`dirname ${BASH_SOURCE[0]}`

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
if [ -f "${SOURCE}/../source/TwainDirectApplication/bin/${MACHINE}/Debug/TwainDirectApplication.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectApplication/bin/${MACHINE}/Debug"
	mono TwainDirectApplication.exe
	exit
fi

# 
# Release...
#
if [ -f "${SOURCE}/../source/TwainDirectApplication/bin/${MACHINE}/Release/TwainDirectApplication.exe" ]; then
	cd "${SOURCE}/../source/TwainDirectApplication/bin/${MACHINE}/Release"
	mono TwainDirectApplication.exe
	exit
fi
