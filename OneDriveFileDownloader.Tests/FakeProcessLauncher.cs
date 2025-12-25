using OneDriveFileDownloader.UI.Services;

namespace OneDriveFileDownloader.Tests
{
    public class FakeProcessLauncher : IProcessLauncher
    {
        public string? LastOpened { get; private set; }
        public bool Open(string path)
        {
            LastOpened = path;
            return true;
        }
    }
}