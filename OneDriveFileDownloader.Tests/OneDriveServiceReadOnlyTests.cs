using System.IO;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class OneDriveServiceReadOnlyTests
    {
        [Fact]
        public void OneDriveService_DoesNotContainWriteHttpMethods()
        {
            // search upwards from the test output folder to find the solution file, then search from repo root
            string? cursor = Directory.GetCurrentDirectory();
            string? repoRoot = null;
            while (cursor != null)
            {
                var slns = Directory.GetFiles(cursor, "*.sln", SearchOption.TopDirectoryOnly);
                if (slns.Length > 0)
                {
                    repoRoot = cursor;
                    break;
                }
                cursor = Directory.GetParent(cursor)?.FullName;
            }
            Assert.False(string.IsNullOrEmpty(repoRoot), "Repository root (solution folder) not found");
            var matches = Directory.GetFiles(repoRoot, "OneDriveService.cs", SearchOption.AllDirectories);
            Assert.True(matches.Length > 0, "OneDriveService.cs not found in repository");
            var src = File.ReadAllText(matches[0]);
            Assert.DoesNotContain("HttpMethod.Post", src);
            Assert.DoesNotContain("HttpMethod.Put", src);
            Assert.DoesNotContain("HttpMethod.Patch", src);
            Assert.DoesNotContain("HttpMethod.Delete", src);
            Assert.DoesNotContain("\"POST\"", src);
            Assert.DoesNotContain("\"PUT\"", src);
            Assert.DoesNotContain("\"PATCH\"", src);
            Assert.DoesNotContain("\"DELETE\"", src);
        }
    }
}
