using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class CodeAuditTests
    {
        [Fact]
        public void Repo_DoesNotContainHttpWriteCalls()
        {
            // locate repository root (solution folder) robustly
            var current = Directory.GetCurrentDirectory();
            string repoRoot = null;
            while (current != null)
            {
                var slns = Directory.GetFiles(current, "*.sln", SearchOption.TopDirectoryOnly);
                if (slns.Length > 0) { repoRoot = current; break; }
                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            if (string.IsNullOrEmpty(repoRoot)) Assert.False(true, "Repository root not found (no solution file)");

            var files = Directory.GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("bin") && !p.Contains("obj") && !p.Contains("\\OneDriveFileDownloader.Tests\\"));

            var forbidden = new[] { "PostAsync(", "PutAsync(", "PatchAsync(", "DeleteAsync(", "HttpMethod.Post", "\"POST\"", "\"PUT\"", "\"PATCH\"", "\"DELETE\"" };
            foreach (var f in files)
            {
                var src = File.ReadAllText(f);
                foreach (var s in forbidden)
                {
                    if (src.Contains(s))
                    {
                        throw new Xunit.Sdk.XunitException($"Forbidden HTTP write usage found in {f}: contains '{s}'");
                    }
                }
            }
        }
    }
}