using Xunit;
using Asterran.Engine;
using System.IO;
using System.Collections.Generic;

namespace Asterran.Engine.Test
{
    public class ArchitectureAnalyzerTests
    {
        [Fact]
        public void ArchitectureAnalyzer_ScansProjectsAndDependencies()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create Project A (Core)
                string coreDir = Path.Combine(tempDir, "CoreProj");
                Directory.CreateDirectory(coreDir);
                string coreProjFile = Path.Combine(coreDir, "CoreProj.csproj");
                File.WriteAllText(coreProjFile, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

                // Create Project B (App - references Core)
                string appDir = Path.Combine(tempDir, "AppProj");
                Directory.CreateDirectory(appDir);
                string appProjFile = Path.Combine(appDir, "AppProj.csproj");
                File.WriteAllText(appProjFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <ProjectReference Include=""..\CoreProj\CoreProj.csproj"" />
                    </ItemGroup>
                </Project>");

                var analyzer = new ArchitectureAnalyzer(tempDir);
                var state = analyzer.GetArchitectureState();

                Assert.Equal(2, state.Count);
                
                var coreNode = state.Find(p => p.ProjectName == "CoreProj");
                var appNode = state.Find(p => p.ProjectName == "AppProj");

                Assert.NotNull(coreNode);
                Assert.NotNull(appNode);
                
                Assert.Contains("CoreProj", appNode.Dependencies);
                Assert.Empty(coreNode.Dependencies);

                // Test file resolution
                string coreFile = Path.Combine(coreDir, "Logger.cs");
                string appFile = Path.Combine(appDir, "Program.cs");

                Assert.Equal("CoreProj", analyzer.GetProjectForFile(coreFile));
                Assert.Equal("AppProj", analyzer.GetProjectForFile(appFile));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void ArchitectureAnalyzer_DetectsAndClearsDocumentationDrift()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create Project
                string coreDir = Path.Combine(tempDir, "CoreProj");
                Directory.CreateDirectory(coreDir);
                string coreProjFile = Path.Combine(coreDir, "CoreProj.csproj");
                File.WriteAllText(coreProjFile, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

                // Create a mock doc folder and a documentation markdown file referencing process spawner / hashing ciphers
                string docsDir = Path.Combine(tempDir, "docs");
                Directory.CreateDirectory(docsDir);
                string mdFile = Path.Combine(docsDir, "SecurityPolicy.md");
                File.WriteAllText(mdFile, "This policy covers execution of command subprocess shell execution, obsolete cryptography algorithms like MD5 hashing and cipher cryptography.");

                // Set file write time to past so it registers as not updated recently
                File.SetLastWriteTime(mdFile, DateTime.Now.AddHours(-1));

                var analyzer = new ArchitectureAnalyzer(tempDir);

                // Simulate editing a C# file introducing new code using similar semantic tokens
                string csharpFile = Path.Combine(coreDir, "ProcessService.cs");
                var diffLines = new List<DiffLine>
                {
                    new DiffLine { Type = "Added", Text = "public void ExecuteSubprocessCommand()" },
                    new DiffLine { Type = "Added", Text = "{" },
                    new DiffLine { Type = "Added", Text = "    // Use MD5 hashing and process spawn execution" },
                    new DiffLine { Type = "Added", Text = "}" }
                };

                analyzer.NotifyFileEdit(csharpFile, 4, 0, diffLines);

                // Verify drift warning is triggered
                var violations = analyzer.GetActiveViolations();
                Assert.Single(violations);
                Assert.Equal("Documentation Drift Guardrail", violations[0].RuleName);
                Assert.Contains("SecurityPolicy.md", violations[0].Message);

                // Simulate documentation update
                // Writing to the file updates its LastWriteTime to DateTime.Now
                File.WriteAllText(mdFile, "Updated: This policy covers subprocess shell execution and obsolete cryptography algorithms MD5.");
                analyzer.NotifyFileEdit(mdFile, 0, 0, new List<DiffLine>());

                // Verify drift warning is cleared automatically
                violations = analyzer.GetActiveViolations();
                Assert.Empty(violations);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
