#!/bin/bash

# Home...
SOURCE=`dirname ${BASH_SOURCE[0]}`

# 
# TwainDirectApplication
#
rm -rf ${SOURCE}/../source/data/TwainDirectApplication/TwainDirectApplication.Log
rm -rf ${SOURCE}/../source/data/TwainDirectApplication/cmd
rm -rf ${SOURCE}/../source/data/TwainDirectApplication/data.txt
rm -rf ${SOURCE}/../source/data/TwainDirectApplication/images/*

# 
# TwainDirectOnTwain
#
rm -rf ${SOURCE}/../source/data/TwainDirectOnTwain/TwainDirectOnTwain.Log
rm -rf ${SOURCE}/../source/data/TwainDirectOnTwain/images/*
rm -rf ${SOURCE}/../source/data/TwainDirectOnTwain/ipc/*

# 
# TwainDirectOnSane
#
rm -rf ${SOURCE}/../source/data/TwainDirectOnSane/TwainDirectOnSane.Log
rm -rf ${SOURCE}/../source/data/TwainDirectOnSane/images/*
rm -rf ${SOURCE}/../source/data/TwainDirectOnSane/ipc/*

# 
# TwainDirectScanner
#
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/TwainDirectScanner.Log
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/filein
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/fileout
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/header.txt
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/list.txt
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/metadata.txt
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/reply.txt
rm -rf ${SOURCE}/../source/data/TwainDirectScanner/task.txt
