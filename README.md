# Multi-Win

**A Windows and Linux bootable USB creator — with a built-in OS catalog, custom ISO detection, and a no-USB Windows Setup mode.**

Multi-Win helps you prepare installation media without juggling several utilities. Choose an image, select a target, review the options, and flash.

[**Download the latest beta**](https://github.com/slimGT-ui/Multi-Win/releases/tag/v0.6.0-beta.1) · [Source code](https://github.com/slimGT-ui/Multi-Win) · [Report a bug](https://github.com/slimGT-ui/Multi-Win/issues)

> **Current release: `v0.6.0-beta.1`**
>
> This is a beta build. USB writing, UEFI boot, and internal-drive Setup have not yet been validated across a range of real hardware. Back up important data and use a spare USB drive for testing.

## Features

- **Built-in Windows catalog** — four simple choices: Windows 10, Windows 10 Pro, Windows 11, and Windows 11 Pro. The two editions of each version share one ISO; Pro selections configure Windows Setup to select the Professional edition.
- **Bring your own ISO** — select a local `.iso`, drag and drop it into the app, or provide a direct ISO download URL.
- **Content-aware ISO detection** — inspects files inside the image (such as Windows Setup files and Linux metadata) instead of relying only on the filename.
- **Windows and Linux USB creation** — Windows installation media is prepared as files; Linux images are written in raw mode.
- **Large Windows image support** — automatically splits an oversized `install.wim` for FAT32, and can convert a large `install.esd` when needed.
- **Two Windows USB file-system modes** — FAT32 with automatic image splitting, or NTFS direct copy without splitting.
- **MBR or GPT partition layout** — choose the partition scheme to match the target machine's boot requirements.
- **Windows 11 setup options** — optional hardware requirement bypasses and unattended setup tweaks.
- **Hardware compatibility check** — displays detected hardware and warns about Windows 11 requirements.
- **Optional USB checks** — bad-block surface test before writing and file/raw verification after writing.
- **USB speed benchmark** — measures sequential write and read speed.
- **Windows Setup from an internal drive** — copies Windows Setup files to an internal drive and configures a one-time boot into Setup. No USB stick required.
- **Progress and diagnostics** — live progress, transfer speed, ETA, detailed operation log, and log export.
- **ISO cache manager** — downloaded images are stored in `%TEMP%\\Multi-Win` and can be cleared from the app.

## Getting started

### Download and run

1. Open the [Multi-Win releases page](https://github.com/slimGT-ui/Multi-Win/releases).
2. Download the `Multi-Win-*-win-x64.zip` asset from the release you want.
3. Extract the ZIP to a folder.
4. Run `Multi-Win.exe` **as administrator**.

The release package is self-contained: you do **not** need to install the .NET runtime separately.

### Create a bootable USB

1. Choose an image from the built-in catalog, browse for a local ISO, or add a direct ISO URL.
2. Select **USB drive** as the target.
3. Choose the correct USB device. Double-check its model and capacity.
4. Select the file system and partition scheme. The defaults are intended as the safer starting point for broad compatibility.
5. Review any optional checks and Windows setup tweaks.
6. Click **Flash** and confirm the operation.
7. Wait for the process to finish. Do not unplug the drive while it is being written.

**The selected USB drive will be erased.** Multi-Win shows a confirmation before writing, but you are responsible for selecting the correct device.

## Target and format options

| Option | What it does |
| --- | --- |
| **FAT32 (split)** | Copies Windows Setup files and splits an oversized `install.wim` into FAT32-compatible chunks. Recommended when broad BIOS/UEFI compatibility matters. |
| **NTFS (direct)** | Copies the Windows ISO contents without splitting the image. Requires BIOS boot or UEFI firmware capable of booting NTFS. |
| **MBR** | Supports legacy BIOS and many UEFI configurations, depending on firmware and file system. |
| **GPT** | Intended for UEFI systems; does not support legacy BIOS boot. |
| **Raw write** | Writes a Linux ISO directly to the USB device, preserving the image's own partition layout. |

The best combination depends on the target computer's firmware. If unsure, start with the default FAT32 + MBR option and test the resulting USB on the target hardware.

## Windows Setup options

For Windows images, Multi-Win can optionally:

- Bypass Windows 11 TPM, Secure Boot, RAM, and CPU checks.
- Skip the online Microsoft account requirement.
- Set up a local user account.
- Skip Windows privacy questions.
- Prevent automatic BitLocker device encryption.
- Apply the available Appraiser compatibility-file tweak.
- Use the current PC's time zone in unattended setup.

These options change Windows Setup behavior. Review the summary before starting and only enable options you understand.

## Install Windows without a USB drive

Select **This PC (no USB stick)** as the target and choose an internal drive with enough free space.

Multi-Win copies Windows Setup files into a `MULTIWIN-SETUP` folder and configures a **one-time** boot entry. The normal default boot entry is left unchanged. This mode supports Windows Setup images only; Linux images still require a USB drive.

After Windows Setup has finished, use **Remove setup** in Multi-Win to remove its temporary boot entry and setup files. Do not manually delete the folder while the temporary boot entry is still in use.

## Checks and logs

- **Bad-block check:** writes and reads a temporary test file before flashing. It can take time and is not a full hardware certification.
- **Verify after flash:** checks copied files or compares the raw USB data with the source ISO.
- **Test speed:** runs a temporary sequential read/write benchmark on the selected USB drive.
- **Save log:** exports the current operation log for troubleshooting.

## Requirements

- Windows 10 or Windows 11, 64-bit.
- Administrator privileges for disk operations and boot configuration.
- A USB drive large enough for the selected image (for USB creation).
- An internet connection when downloading an image from the built-in catalog or a URL.

For building from source, install the **.NET 8 SDK**.

## Build from source

This is a WPF application targeting .NET 8 for Windows.

```powershell
git clone https://github.com/slimGT-ui/Multi-Win.git
cd Multi-Win

dotnet restore MultiWin.csproj
dotnet build MultiWin.csproj
```

To publish a self-contained Windows x64 build:

```powershell
dotnet publish MultiWin.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

The publish output includes the app's `Data` and `Assets` content. Keep those folders with the executable when distributing a locally built output.

### Custom icons

Place your image files in `Assets/`:

- `windows.png` — Windows image icon.
- `linux.png` — Linux image icon.
- `app.ico` — application icon.
- `app-logo.png` — optional in-app header logo.

## Image sources

The built-in catalog is maintained in [`Data/catalog.json`](Data/catalog.json). The current Windows entries use third-party Archive.org mirrors. Check the source and integrity of an ISO before using it. Multi-Win does not guarantee the availability, authenticity, or safety of third-party downloads.

## Safety

Multi-Win performs low-level disk operations and can permanently erase data on the selected target. Always:

- Back up anything important before using the app.
- Verify the selected drive by its model and capacity.
- Use a spare USB drive while testing beta builds.
- Keep a separate backup before changing boot configuration or installing Windows from an internal drive.
- Never interrupt a write operation or disconnect the target drive mid-process.

Use the software at your own risk. The project is provided without warranty.

## License

[MIT](LICENSE) © 2026 slim.
