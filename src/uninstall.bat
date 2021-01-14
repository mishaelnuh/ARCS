@echo off
SET PYLOCATION5="%AppData%\McNeel\Rhinoceros\5.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib\acorn_spraying\"
SET PYLOCATION6="%AppData%\McNeel\Rhinoceros\6.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib\acorn_spraying\"
SET GHLOCATION="%AppData%\Grasshopper\Libraries\ACORNSpraying.ghpy"

echo PLEASE ENSURE RHINO IS NOT RUNNING.
pause

echo.

IF EXIST %PYLOCATION5% (
echo Deleting acorn_spraying python library for Rhino 5.
@RD /S /Q %PYLOCATION5%
)

IF EXIST %PYLOCATION6% (
echo Deleting acorn_spraying python library for Rhino 6.
@RD /S /Q %PYLOCATION6%
)

IF EXIST %GHLOCATION% (
echo Deleting acorn_spraying GH components for Rhino 6.
del %GHLOCATION%
)

echo.

echo UNINSTALL FINISHED.
pause