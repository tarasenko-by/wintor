# Security policy

Wintor talks to the internet all the time — peers, trackers, DHT — and parses files and links that come from
strangers. Security reports are taken seriously.

## Supported versions

Only the latest release (and `main`) gets fixes. Wintor is pre-1.0, so there are no long-term branches.

## Reporting a vulnerability

**Do not open a public issue.** Use GitHub's private reporting instead:
the repository's **Security** tab → **Report a vulnerability**. Only the maintainers see the report.

Please include what an attacker can do, the steps or a sample file to reproduce it, and the Wintor version.
You will get an answer within a few days. Once a fix is released, the advisory is published and you are
credited unless you prefer not to be.

## What counts

Examples of what we want to hear about:

- A `.torrent` file, magnet link, peer or tracker response that crashes Wintor, hangs it or uses unbounded memory.
- Files written outside the chosen download folder (path traversal through file names in a torrent).
- Code execution, or a way to make Wintor open or run a downloaded file without the user asking.
- Leaks of the user's data beyond what the BitTorrent protocol itself shares (your IP address is visible to peers
  by design).

Bugs in the BitTorrent engine itself may belong upstream in [MonoTorrent](https://github.com/alanmcgovern/monotorrent);
report them here anyway and we will forward them.
