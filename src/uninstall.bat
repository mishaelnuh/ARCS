@echo off
SET PYLOCATION="%AppData%\McNeel\Rhinoceros\6.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib\acorn_spraying\"
SET GHLOCATION="%AppData%\Grasshopper\Libraries\ACORNSpraying.ghpy"

echo PLEASE ENSURE RHINO IS NOT RUNNING.
pause

echo.

IF EXIST %PYLOCATION% (
echo Deleting acorn_spraying python library.
@RD /S /Q %PYLOCATION%
)

IF EXIST %GHLOCATION% (
echo Deleting acorn_spraying GH components.
del %GHLOCATION%
)

echo.

echo UNINSTALL FINISHED.
pause