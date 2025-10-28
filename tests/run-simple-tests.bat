@echo off
echo ========================================
echo OpcDAToMSA 依赖注入重构测试验证
echo ========================================

echo.
echo 正在编译测试验证器...

REM 编译测试验证器
csc /target:exe /out:TestValidator.exe TestValidator.cs /reference:"..\bin\Debug\OpcDAToMSA.exe" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll"

if %ERRORLEVEL% neq 0 (
    echo 编译失败！请检查依赖项。
    pause
    exit /b 1
)

echo.
echo 正在运行测试验证...

REM 运行测试验证器
TestValidator.exe

echo.
echo 清理临时文件...
del TestValidator.exe

echo.
echo ========================================
echo 测试验证完成！
echo ========================================
pause
