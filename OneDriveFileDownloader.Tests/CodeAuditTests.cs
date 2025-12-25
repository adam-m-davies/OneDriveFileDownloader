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
            // scan all .cs files in repo for HTTP write related strings
            var root = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName; // repo root from tests bin
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("bin") && !p.Contains("obj") && !p.Contains("\\OneDriveFileDownloader.Tests\\"));

            var forbidden = new[] { "PostAsync(", "PutAsync(", "PatchAsync(", "DeleteAsync(", "HttpMethod.Post", "\"POST\"", "\"PUT\"", "\"PATCH\"", "\"DELETE\"" };
            foreach (var f in files)
            {
                var src = File.ReadAllText(f);
                foreach (var s in forbidden)
                {
                    if (src.Contains(s))
                    {
                        Assert.False(true, $"Forbidden HTTP write usage found in {f}: contains '{s}'");
                    }
                }
            }
        }
    }
}