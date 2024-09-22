SETLOCAL

set "SOLUTION_DIR=%1"
set "TARGET_DIR=%2"
set "TARGET_NAME=%3"
set "TARGET_EXT=%4"
set "CONFIGURATION=%5"
set "PLATFORM_SHORT_NAME=%6"

set TARGET_PLUGIN_DIR="%SOLUTION_DIR%\..\..\..\Packages\com.deltation.aaaa-rp\Runtime\BindlessPlugin\Plugins\"
echo Target Plugin Dir is %TARGET_PLUGIN_DIR%

if %PLATFORM_SHORT_NAME% == "x86" (
    set TARGET_PLUGIN_PATH=%TARGET_PLUGIN_DIR%x86
) else (
    set TARGET_PLUGIN_PATH=%TARGET_PLUGIN_DIR%x86_64
)

echo Target Plugin Path is %TARGET_PLUGIN_PATH%
mkdir %TARGET_PLUGIN_PATH%
echo "%TARGET_PLUGIN_PATH%\%TARGET_NAME%"
copy /Y "%TARGET_DIR%%TARGET_NAME%%TARGET_EXT%" "%TARGET_PLUGIN_PATH%"

if %CONFIGURATION% == "Debug" (
    copy /Y "%TARGET_DIR%%TARGET_NAME%.pdb" %TARGET_PLUGIN_PATH%
)

ENDLOCAL