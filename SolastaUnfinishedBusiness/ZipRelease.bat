@echo off
echo Packaging the release to SolastaUnfinishedBusiness.zip
rem powershell -ExecutionPolicy Bypass -File ".\ZipRelease.ps1"
echo Copy release info.
copy Info.json "obj\Release Install\SolastaUnfinishedBusiness" /y
copy ChangelogHistory.txt "obj\Release Install\SolastaUnfinishedBusiness" /y
cd "obj\Release Install"
echo Copy mod files
copy SolastaUnfinishedBusiness.dll SolastaUnfinishedBusiness /y
copy SolastaUnfinishedBusiness.pdb SolastaUnfinishedBusiness /y
echo Zip release
tar -acf "SolastaUnfinishedBusiness.zip" SolastaUnfinishedBusiness