# Windows Auto Unzipper

**Automatially unzip files added to a specified directory**

Tired of having to manually extract every zip file you download? Target your downloads folder and let this useful tool take care of it for you in the background.

## Features
- Change which folder the program watches for new zip files
- Configure which archive extensions are watched, such as `.zip`, `.7z`, or `.rar`
- Automatically delete the archive after the extraction has completed
- Launches automatically in the system tray when Windows starts
- Enable or disable the program from the system tray right-click menu
- Settings adjustable through the settings UI

## Build
This version targets Windows 11 with WinForms on .NET 8.

```powershell
dotnet restore
dotnet build .\WindowsAutoUnzipper.sln
```

## Screenshots
![Settings Window](https://imgur.com/0YoOIEk.png)

![Settings Window](https://i.imgur.com/VFzltHU.png)
