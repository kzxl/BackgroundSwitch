@echo off
echo ======= AUTO WALLPAPER CHANGER PUBLISH (.NET 10) =======
echo 1. Build FULL (Self-contained, khong can cai .NET Runtime)
echo 2. Build LITE (Framework-dependent, can cai .NET 10 Runtime)
echo =========================================================
echo Dang build phien ban FULL...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/full
echo.
echo Dang build phien ban LITE...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish/lite
echo.
echo Hoan thanh! File publish nam trong thu muc publish/full va publish/lite
pause
