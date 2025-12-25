using System.IO;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class OneDriveServiceReadOnlyTests
    {
        [Fact]
        public void OneDriveService_DoesNotContainWriteHttpMethods()
        {
            var path = Path.Combine("..", "OneDriveFileDownloader.Core", "Services", "OneDriveService.cs");
            Assert.True(File.Exists(path), "Source file not found: " + path);
            var src = File.ReadAllText(path);
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
