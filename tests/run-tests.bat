@echo off
echo ========================================
echo OpcDAToMSA 单元测试运行脚本
echo ========================================

echo.
echo 正在检查测试环境...

REM 检查是否安装了 xUnit
if not exist "packages\xunit.2.4.1" (
    echo 正在安装 xUnit 测试框架...
    nuget restore tests\packages.config
)

echo.
echo 正在编译测试项目...

REM 编译测试项目
msbuild tests\OpcDAToMSA.Tests.csproj /p:Configuration=Debug /verbosity:minimal

if %ERRORLEVEL% neq 0 (
    echo 编译失败！
    pause
    exit /b 1
)

echo.
echo 正在运行单元测试...

REM 运行测试
packages\xunit.runner.console.2.4.1\tools\net452\xunit.console.exe tests\bin\Debug\OpcDAToMSA.Tests.dll

echo.
echo ========================================
echo 测试完成！
echo ========================================
pause
