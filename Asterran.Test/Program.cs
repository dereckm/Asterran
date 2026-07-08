using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asterran.Engine;

namespace Asterran.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  ASTERRAN MULTI-LANGUAGE GUARDRAILS VERIFICATION ");
            Console.WriteLine("==================================================");

            string testWorkspace = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestWorkspace");
            
            // Prepare clean test folder
            if (Directory.Exists(testWorkspace))
            {
                Directory.Delete(testWorkspace, true);
            }
            Directory.CreateDirectory(testWorkspace);

            // Create Mock Brain Logs Folder
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string mockBrainLogsDir = Path.Combine(userProfile, ".gemini", "antigravity", "brain", "test-convo-id", ".system_generated", "logs");
            
            if (Directory.Exists(mockBrainLogsDir))
            {
                Directory.Delete(mockBrainLogsDir, true);
            }
            Directory.CreateDirectory(mockBrainLogsDir);
            
            string mockLogFile = Path.Combine(mockBrainLogsDir, "transcript.jsonl");
            
            // Write initial conversation line
            AppendLogLine(mockLogFile, "USER_EXPLICIT", "USER_INPUT", "Audit the multi-language codebase.");
            
            // Create Mock C# Project Structure
            // Project A: AuthProject
            string authProjDir = Path.Combine(testWorkspace, "AuthProject");
            Directory.CreateDirectory(authProjDir);
            string authProjFile = Path.Combine(authProjDir, "AuthProject.csproj");
            File.WriteAllText(authProjFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            // Project B: DataProject (references AuthProject)
            string dataProjDir = Path.Combine(testWorkspace, "DataProject");
            Directory.CreateDirectory(dataProjDir);
            string dataProjFile = Path.Combine(dataProjDir, "DataProject.csproj");
            File.WriteAllText(dataProjFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\AuthProject\AuthProject.csproj"" />
  </ItemGroup>
</Project>");

            Console.WriteLine($"Test Workspace: {testWorkspace}");
            var engine = new EngineController(testWorkspace, "test-convo-id");

            // Wire up event logging
            engine.OnArchitectureUpdate += (s, projects) =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[UML Map Update] {DateTime.Now:HH:mm:ss} - C# Project Node Statuses:");
                foreach (var p in projects)
                {
                    string deps = string.Join(", ", p.Dependencies);
                    Console.WriteLine($"  Project: {p.ProjectName,-15} | Status: {p.Status,-8}");
                    if (p.FlagReason != null)
                    {
                        Console.WriteLine($"    ↳ GUARDRAIL WARNING: {p.FlagReason}");
                    }
                }
                Console.ResetColor();
                Console.WriteLine("---------------------------------------------");
            };

            // Start Engine
            engine.Start();
            await Task.Delay(1000);

            // ==========================================
            // TEST 1: C# Secrets Guardrail
            // ==========================================
            string csFilePath = Path.Combine(authProjDir, "Logger.cs");
            Console.WriteLine("\n--- TEST 1: Proposing C# file with hardcoded API key (Secrets Guardrail) ---");
            AppendLogLineWithToolCall(mockLogFile, "MODEL", "PLANNER_RESPONSE", "Adding API key to C#.", "replace_file_content", csFilePath);
            await Task.Delay(1500);

            string[] csSecretCode = new string[]
            {
                "using System;",
                "namespace AuthProject {",
                "    public class Logger {",
                "        private string apiKey = \"sec_key_abc123xyz789\"; // Secrets Violation!",
                "    }",
                "}"
            };
            File.WriteAllLines(csFilePath, csSecretCode);
            await Task.Delay(2500);

            // Clean up Test 1 to return to Green
            Console.WriteLine("\n--- Cleaning up Test 1 (Removing C# API key) ---");
            string[] csCleanCode = new string[]
            {
                "using System;",
                "namespace AuthProject {",
                "    public class Logger {",
                "        private string config = \"normal\";",
                "    }",
                "}"
            };
            File.WriteAllLines(csFilePath, csCleanCode);
            await Task.Delay(2500);

            // Verify manual clear under new guardrail rules
            var activeViolations = engine.GetActiveViolations();
            if (activeViolations.Count > 0)
            {
                Console.WriteLine($"\n--- Developer clears the active C# violation: '{activeViolations[0].Message}' ---");
                engine.ClearViolation(activeViolations[0].Id);
            }
            await Task.Delay(2500);

            // ==========================================
            // TEST 2: Python Command Execution Guardrail
            // ==========================================
            string pyFilePath = Path.Combine(dataProjDir, "helper.py");
            Console.WriteLine("\n--- TEST 2: Proposing Python file with subprocess process call (Command Execution Guardrail) ---");
            AppendLogLineWithToolCall(mockLogFile, "MODEL", "PLANNER_RESPONSE", "Adding system call to Python helper.", "write_to_file", pyFilePath);
            await Task.Delay(1500);

            string[] pyCode = new string[]
            {
                "import subprocess",
                "def run_cleaning():",
                "    # Command Execution Violation!",
                "    subprocess.run(['rm', '-rf', '/tmp/cache'])"
            };
            File.WriteAllLines(pyFilePath, pyCode);
            await Task.Delay(2500);

            // ==========================================
            // TEST 3: JavaScript Obsolete Cryptography Guardrail
            // ==========================================
            string jsFilePath = Path.Combine(dataProjDir, "crypto.js");
            Console.WriteLine("\n--- TEST 3: Proposing JavaScript file with MD5 usage (Obsolete Cryptography Guardrail) ---");
            AppendLogLineWithToolCall(mockLogFile, "MODEL", "PLANNER_RESPONSE", "Adding hashing utility in JS.", "write_to_file", jsFilePath);
            await Task.Delay(1500);

            string[] jsCode = new string[]
            {
                "const crypto = require('crypto');",
                "function hashPassword(pwd) {",
                "    // Obsolete Cryptography MD5 Violation!",
                "    return crypto.createHash('md5').update(pwd).digest('hex');",
                "}"
            };
            File.WriteAllLines(jsFilePath, jsCode);
            await Task.Delay(2500);

            // Stop Engine
            Console.WriteLine("Stopping engine...");
            engine.Stop();

            Console.WriteLine("\nVerification completed successfully.");
            
            // Clean up
            try
            {
                Directory.Delete(testWorkspace, true);
                Directory.Delete(mockBrainLogsDir, true);
            }
            catch {}
        }

        static void AppendLogLine(string filePath, string source, string type, string content)
        {
            var logEntry = new
            {
                step_index = 0,
                source = source,
                type = type,
                status = "DONE",
                created_at = DateTime.UtcNow.ToString("o"),
                content = content
            };
            string json = System.Text.Json.JsonSerializer.Serialize(logEntry);
            File.AppendAllText(filePath, json + Environment.NewLine);
        }

        static void AppendLogLineWithToolCall(string filePath, string source, string type, string content, string toolName, string targetFile)
        {
            var logEntry = new
            {
                step_index = 0,
                source = source,
                type = type,
                status = "DONE",
                created_at = DateTime.UtcNow.ToString("o"),
                content = content,
                tool_calls = new[]
                {
                    new
                    {
                        name = toolName,
                        args = new
                        {
                            TargetFile = targetFile,
                            toolSummary = $"Writing to {Path.GetFileName(targetFile)}"
                        }
                    }
                }
            };
            string json = System.Text.Json.JsonSerializer.Serialize(logEntry);
            File.AppendAllText(filePath, json + Environment.NewLine);
        }
    }
}
