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
    }
}
