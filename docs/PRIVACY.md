# Wintor privacy policy

_Last updated: October 24, 2026_

Wintor is a BitTorrent client. It is open source (GPL-3.0-or-later); you can read exactly what it does at
<https://github.com/tarasenko-by/wintor>.

## What Wintor does not do

- **No telemetry, analytics, crash reporting or ads.** Wintor does not send anything about you or your use of it to
  its authors or to anyone else.
- **No accounts.** There is nothing to sign in to.
- **No content catalog or search.** Wintor only downloads what you add yourself.

## What stays on your device

Your settings, the list of torrents, their progress and resume data are stored in Wintor's own folder on your PC
(`%LOCALAPPDATA%\Packages\…\LocalState`). They never leave your device and are removed when you uninstall Wintor.
Downloaded files are saved in the folder you choose.

## What the BitTorrent protocol shares

Downloading and sharing torrents means connecting to other computers. This is how BitTorrent works, in every
client:

- **Peers** you exchange data with see your IP address and which pieces of the torrent you have.
- **Trackers** listed in a torrent, and the **DHT** network, learn your IP address, your listening port and which
  torrents you take part in.
- With **UPnP / NAT-PMP**, Wintor asks your router to open the listening port shown in Settings.

Wintor talks only to the peers, trackers and DHT nodes of the torrents you add. If you need your IP address hidden,
use a VPN you trust; Wintor itself does not hide it.

## Links

Links in Wintor (GitHub, Microsoft Store, this policy) open in your browser, where those sites' own policies apply.
Windows notifications ("download finished") are shown by Windows on your device.

## Your responsibility

Download and share only content you have the right to — for example Linux distributions, open-source software and
public-domain or Creative Commons works.

## Contact

Questions about this policy: open an issue at <https://github.com/tarasenko-by/wintor/issues>. Security problems:
see [SECURITY.md](../SECURITY.md).
