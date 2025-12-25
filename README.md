# OneDriveFileDownloader

A .NET 10 class library and sample console app to sign-in with a personal Microsoft account and download unique video files from a shared OneDrive folder.

Highlights
- Uses Microsoft.Identity.Client (MSAL) for interactive and device-code authentication (personal accounts supported)
- Calls Microsoft Graph REST endpoints to enumerate `sharedWithMe`, list children and download content
- Keeps track of previously downloaded files by SHA-1 using an abstract `IDownloadRepository` and a default SQLite implementation
- No third-party libraries (only Microsoft packages)

Quick start
1. Register an app in Azure AD (App registrations) that allows personal Microsoft accounts ("Accounts in any organizational directory and personal Microsoft accounts").
   - Supported account types: *Accounts in any organizational directory and personal Microsoft accounts (e.g., Skype, Xbox)*.
   - Redirect URIs: add **one or both** of the following to your app registration:
     - `http://localhost` (add as **Web** redirect URI)
     - `https://login.microsoftonline.com/common/oauth2/nativeclient` (add via **Mobile and desktop applications** platform)
     - Note: the code uses `http://localhost` explicitly for interactive flow. Ensure that the value you register matches the app's Client ID and the redirect URI used by the app.
   - Note the **Client ID**.
   - Add delegated permission: `Files.Read` and `User.Read` (user consent required at runtime).
2. Build:
   dotnet build
3. Run the console sample and supply your ClientId when prompted. Follow the interactive sign-in (system browser or device code). Select a shared folder to scan and a local folder to save downloads.

Repository structure
- `OneDriveFileDownloader.Core` – library containing `IOneDriveService`, `IDownloadRepository`, `SqliteDownloadRepository`, and the OneDrive file logic
- `OneDriveFileDownloader.UI` – shared UI logic and ViewModels (MVVM)
- `OneDriveFileDownloader.Avalonia` – cross-platform desktop UI using Avalonia UI
- `OneDriveFileDownloader.Console` – small console app that exercises the library

Extensibility
- To use a different database/backing store implement `IDownloadRepository` and pass that into your consumer code.

Security notes
- This sample uses delegated permissions and interactive sign-in. For production, consider more robust token management and secure storage.

License
MIT (sample code)
