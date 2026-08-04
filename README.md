<p align="center">
  <img src="https://avatars.githubusercontent.com/u/103479527" width="128px" height="128px" alt="Mica For Everyone, two blue squares logo">
</p>
<h1 align="center">Mica For Everyone!</h1>

# Fork Note
**MicaForEveryone with some fixes for Windows 10 users**  
- [Fix Win10 window-state transparency refresh](https://github.com/MicaForEveryone/MicaForEveryone/commit/b32e3e21c1e4cb533c36337df8148655fd7141cd)  
Prevent apps from losing transparency when maximized/minimized   
- [Fix Win10 extended frame edge overspill](https://github.com/MicaForEveryone/MicaForEveryone/commit/173ee3fd45221b3ad787a9ffe1d413e09f5f3308)  
Fix the left, right, & bottom edges of windows from having extended transparency effect  
- [Fix Notepad popups & add exclude classes option to process rules](https://github.com/MicaForEveryone/MicaForEveryone/commit/7863e536d31cc718f2e16edaca21b77f4576b93f)  
Fixed all notepad popups from being bright.    
Additionally added that exclude classes ability as an option to all other process rules.   

## Info (Please Read)
> [!NOTE]
> My options for release were either require you to enable Windows Developer Mode to use, or I pay for a real code-signing certificate, or just self-sign `.cer` + `.msix` or `.appinstaller` installers. Obvious choice is self-sign by my certificate or your own.   
>**Just understand that Windows will warn you multiple times about the risks of installing certificates.** 

### If you want your own signing identity
- Fork the repo
- Change the package identity/publisher in `MicaForEveryone.App/Package.appxmanifest`  
- Sign the `MSIX` with your own certificate  
- Update the URLs inside `MicaForEveryoneFork.appinstaller` to point to your own GitHub release files  

### Files
- `MicaForEveryoneFork.cer`:  
   Public certificate used to let Windows trust this self-signed MSIX package.  

- `MicaForEveryoneFork-2.0.7.0-x64.msix`:  
   The actual installable app package.  

- `MicaForEveryoneFork.appinstaller`:  
   Installer with update ability that points to this repo's GitHub releases, or your own if you did your own signing identity.  

## Required for Window Transparency on Win10
#### [DWMBlurGlass](https://github.com/Maplespe/DWMBlurGlass) 
For any apps that DWMBlurGlass works on, MicaForEveryone just simply extends what DWM does to title bars, into the client's frame. If DWM doesn't apply for an app then Mica can only work for it if the app has true-transparency support either built in, add-on/mod, or via patch. 

## Install
- Install files from [Releases](https://github.com/landnthrn/MicaForEveryone/releases)   
- Open `MicaForEveryoneFork.cer`  
  - Choose 'Local Machine' as the install point
  - When prompted choose 'Place all certificates in the following store'  
  - Browse and select 'Trusted Root Certification Authorities' as the Certificate Store
- Use the `MicaForEveryoneFork-2.0.7.0-x64.msix` or `MicaForEveryoneFork.appinstaller`

You're `settings.json` is created at `\Users\%USERNAME%\AppData\Local\Packages\MicaForEveryone.Fork_nk222gb55598j\LocalState`
If you import a `settings.json` made from upstream Mica you should add `#32770` as an exclusion class for global and notepad process rule, this way the notepad popups brightness fix will remain.  

---

> [!CAUTION]
>
> **PHISHING ALERT!** The website micaforeveryone[.]com is **NOT AFFILIATED WITH THE MICA FOR EVERYONE MAINTAINERS**,
> and appears to redirect users to sketchy download sites. The `MicaForEveryone/MicaForEveryone`
> GitHub repository and Microsoft Store are the **ONLY** official download sources and project page for Mica For Everyone.

**Mica For Everyone** is a tool to customize system backdrop on Win32 apps using [DwmSetWindowAttribute](https://docs.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute) and other methods.
It can apply Mica (or any other backdrop materials) on the non-client area (window frame) or background of supported apps and its behavior is customizable through a GUI and a config file.

> [!NOTE]
> You are viewing the WinUI 3 rewrite branch (2.x). For the source code of the older 1.x releases, please see [the `master` branch](https://github.com/MicaForEveryone/MicaForEveryone/tree/master) instead.

> [!NOTE]
> Mica For Everyone is not responsible for rendering the effects you set, it just asks Windows to do that for you. If there's any problem with the effects it's a third-party issue. Try creating a rule for the affected apps and try different settings before opening an issue for it.

## 🕹 How do I get it?
- [Microsoft Store](https://apps.microsoft.com/detail/9P8V68P4Z78P?cid=mfegithubreadme), or
- [App Installer sideload](https://micaforeveryone.github.io/MicaForEveryone.appinstaller)
