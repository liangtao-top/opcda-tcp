using System;
using System.IO;
using System.Text;

namespace OpcDAToMSA.Tests.Unit
{
    /// <summary>
    /// 测试报告生成器
    /// </summary>
    public class TestReportGenerator
    {
        /// <summary>
        /// 生成测试报告
        /// </summary>
        /// <param name="testResults">测试结果</param>
        /// <returns>测试报告HTML</returns>
        public static string GenerateHtmlReport(TestResults testResults)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <title>OpcDAToMSA 依赖注入重构测试报告</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("        .header { background-color: #f0f0f0; padding: 20px; border-radius: 5px; }");
            html.AppendLine("        .summary { margin: 20px 0; }");
            html.AppendLine("        .test-class { margin: 20px 0; border: 1px solid #ddd; border-radius: 5px; }");
            html.AppendLine("        .test-class-header { background-color: #e0e0e0; padding: 10px; font-weight: bold; }");
            html.AppendLine("        .test-method { padding: 10px; border-bottom: 1px solid #eee; }");
            html.AppendLine("        .passed { color: green; }");
            html.AppendLine("        .failed { color: red; }");
            html.AppendLine("        .skipped { color: orange; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // 头部
            html.AppendLine("    <div class=\"header\">");
            html.AppendLine("        <h1>OpcDAToMSA 依赖注入重构测试报告</h1>");
            html.AppendLine("        <p>生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</p>");
            html.AppendLine("    </div>");
            
            // 摘要
            html.AppendLine("    <div class=\"summary\">");
            html.AppendLine("        <h2>测试摘要</h2>");
            html.AppendLine("        <p>总测试数: " + testResults.TotalTests + "</p>");
            html.AppendLine("        <p>通过: <span class=\"passed\">" + testResults.PassedTests + "</span></p>");
            html.AppendLine("        <p>失败: <span class=\"failed\">" + testResults.FailedTests + "</span></p>");
            html.AppendLine("        <p>跳过: <span class=\"skipped\">" + testResults.SkippedTests + "</span></p>");
            html.AppendLine("        <p>成功率: " + (testResults.TotalTests > 0 ? (testResults.PassedTests * 100.0 / testResults.TotalTests).ToString("F2") : "0") + "%</p>");
            html.AppendLine("    </div>");
            
            // 测试详情
            html.AppendLine("    <div class=\"test-details\">");
            html.AppendLine("        <h2>测试详情</h2>");
            
            foreach (var testClass in testResults.TestClasses)
            {
                html.AppendLine("        <div class=\"test-class\">");
                html.AppendLine("            <div class=\"test-class-header\">" + testClass.ClassName + "</div>");
                
                foreach (var testMethod in testClass.TestMethods)
                {
                    html.AppendLine("            <div class=\"test-method\">");
                    html.AppendLine("                <span class=\"" + testMethod.Status.ToString().ToLower() + "\">" + testMethod.MethodName + "</span>");
                    if (!string.IsNullOrEmpty(testMethod.ErrorMessage))
                    {
                        html.AppendLine("                <br><small>" + testMethod.ErrorMessage + "</small>");
                    }
                    html.AppendLine("            </div>");
                }
                
                html.AppendLine("        </div>");
            }
            
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        /// <summary>
        /// 保存测试报告到文件
        /// </summary>
        /// <param name="testResults">测试结果</param>
        /// <param name="filePath">文件路径</param>
        public static void SaveReportToFile(TestResults testResults, string filePath)
        {
            var html = GenerateHtmlReport(testResults);
            File.WriteAllText(filePath, html, Encoding.UTF8);
        }
    }
    
    /// <summary>
    /// 测试结果
    /// </summary>
    public class TestResults
    {
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public int SkippedTests { get; set; }
        public TestClass[] TestClasses { get; set; }
    }
    
    /// <summary>
    /// 测试类
    /// </summary>
    public class TestClass
    {
        public string ClassName { get; set; }
        public TestMethod[] TestMethods { get; set; }
    }
    
    /// <summary>
    /// 测试方法
    /// </summary>
    public class TestMethod
    {
        public string MethodName { get; set; }
        public TestStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// 测试状态
    /// </summary>
    public enum TestStatus
    {
        Passed,
        Failed,
        Skipped
    }
}
