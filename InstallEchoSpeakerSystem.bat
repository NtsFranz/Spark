@echo off
set latestURL=%1
set latestVer=%2
set shouldStartESS=%3
echo.    
echo Downloading and installing Echo Speaker System...
mkdir "C:\Temp" > nul 2> nul
curl.exe -L -o C:\Temp\%latestVer%.zip "%latestURL%" > nul 2> nul
REM GOTO endscript
mkdir "C:\Program Files (x86)\Echo Speaker System" > nul 2> nul
setlocal
set dwnld=0;
cd /d %~dp0
for %%a in (C:\Temp\%latestVer%.zip) do (
    Call :UnZipFile "C:\Program Files (x86)\Echo Speaker System\%%~na\" "C:\Temp\%%~nxa"
)
exit /b

:UnZipFile <ExtractTo> <newzipfile>
set vbs="%temp%\_.vbs"
if exist %vbs% del /f /q %vbs%
>%vbs%  echo Set fso = CreateObject("Scripting.FileSystemObject")
>>%vbs% echo If NOT fso.FolderExists(%1) Then
>>%vbs% echo fso.CreateFolder(%1)
>>%vbs% echo End If
>>%vbs% echo set objShell = CreateObject("Shell.Application")
>>%vbs% echo set FilesInZip=objShell.NameSpace(%2).items
>>%vbs% echo objShell.NameSpace(%1).CopyHere(FilesInZip)
>>%vbs% echo Set fso = Nothing
>>%vbs% echo Set objShell = Nothing
cscript //nologo %vbs%
if exist %vbs% del /f /q %vbs%

sc query VirtualAudioCable_83ed7f0e-2028-4956-b0b4-39c76fdaef1d | find "does not exist" > nul 2> nul
if %ERRORLEVEL% EQU 0 set VACINST=0
if %ERRORLEVEL% EQU 1 set VACINST=1

if %dwnld%==1 GOTO cont
set /A dwnld = 1

if %VACINST%==1 GOTO contnoVAC

echo Downloading and installing Virtual Audio Cable...
@echo off
curl.exe -o C:\Temp\vac464lite.zip https://software.muzychenko.net/freeware/vac464lite.zip > nul 2> nul
@echo off
setlocal
cd /d %~dp0
for %%a in (C:\Temp\vac464lite.zip) do (
    Call :UnZipFile "C:\Temp\%%~na\" "C:\Temp\%%~nxa"
)
exit /b

:cont
del "C:\Temp\vac464lite.zip" > nul 2> nul
"C:\Temp\vac464lite\setup64.exe"
del "C:\Temp\vac464lite.zip" > nul 2> nul
rd /s /q "C:\Temp\vac464lite" > nul 2> nul

:contnoVAC
@echo off
taskkill /IM "Echo Speaker System.exe" > nul 2> nul
del C:\Temp\"%latestVer%".zip > nul 2> nul
REM taskkill /IM "Echo Speaker System.exe" /F

@echo off
del "%userprofile%\Start Menu\Programs\Echo Speaker System.lnk" > nul 2> nul
REM mklink "%userprofile%\Start Menu\Programs\Echo Speaker System.lnk" "C:\Program Files (x86)\Echo Speaker System\v0.3.1\Echo Speaker System.exe"
@echo off
set SCRIPT="%TEMP%\%RANDOM%-%RANDOM%-%RANDOM%-%RANDOM%.vbs"
echo Set oWS = WScript.CreateObject("WScript.Shell") >> %SCRIPT%
echo sLinkFile = "%userprofile%\Start Menu\Programs\Echo Speaker System.lnk" >> %SCRIPT%
echo Set oLink = oWS.CreateShortcut(sLinkFile) >> %SCRIPT%
echo oLink.TargetPath = "C:\Program Files (x86)\Echo Speaker System\%latestVer%\Echo Speaker System.exe" >> %SCRIPT%
echo oLink.Arguments = "" >> %SCRIPT%
echo oLink.Save >> %SCRIPT%
cscript /nologo %SCRIPT%
del %SCRIPT%

FOR /d %%a IN ("C:\Program Files (x86)\Echo Speaker System\*") DO IF /i NOT "%%~nxa"=="%latestVer%" RD /S /Q "%%a" > nul 2> nul
echo.
echo.
echo Echo Speaker System succesfully installed!
if %VACINST%==0 GOTO needsrestart

if %shouldStartESS%==0 GOTO endscript
Start "" "C:\Program Files (x86)\Echo Speaker System\%latestVer%\Echo Speaker System.exe"
GOTO endscript

:needsrestart
echo Restart required before using Echo Speaker System!
REM exit

:endscript
rem exit




