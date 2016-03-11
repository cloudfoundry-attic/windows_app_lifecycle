@echo off
set build_path=%1
set cache_path=%2
:: do whatever is needed for the application to run

echo Nothing to do ...
echo No error 1>&2
echo touch > %build_path%\compite-touch.txt
echo touch > %cache_path%\compite-touch.txt
echo Done.

exit /b 0
