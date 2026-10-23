# Releasing to the Microsoft Store

The Store signs the package itself, so no certificate is needed. Steps in **bold** happen in Partner Center and only
the account owner can do them.

## Once, before the first release

Done on 2026-10-24: the name "Wintor - Torrent Client" is reserved (Store ID `9P7P2DH9P4XN`,
<https://apps.microsoft.com/detail/9P7P2DH9P4XN>), the manifest carries its identity and version 0.2.0.0, and
About links to the Store. Kept here for a future fork or a second app.

1. **Register as an individual developer** at <https://partner.microsoft.com/dashboard> (free for individuals).
2. **Reserve the name**: Apps and games → New product → MSIX or PWA app → "Wintor". If it is taken, pick the new
   name everywhere (manifest, strings `AppDisplayName`, README, TRADEMARKS.md) before going further.
3. **Copy the package identity**: the product → Product management → Product identity. Put its values into
   `src/Wintor.App/Package.appxmanifest`:
   - `Identity Name="…"` ← Package/Identity/Name
   - `Identity Publisher="CN=…"` ← Package/Identity/Publisher
   - `PublisherDisplayName` ← Package/Properties/PublisherDisplayName
4. Put the Store ID (12 characters, e.g. `9N…`) into `SettingsViewModel.StoreProductId`: About then shows the
   Microsoft Store link.
5. In the same commit set `Version="0.2.0.0"` (the development manifest still says 1.0.0.0, while "What's new" and
   the README call this 0.2). Do it together with the new identity: to Windows that is a different app, so the
   development install keeps its data. Lowering the version under the old identity would force a reinstall that
   wipes it.

## Every release

1. Raise `Version` in `Package.appxmanifest` (`major.minor.build.0`; the Store needs the last part 0 and every
   upload higher than the previous one). Add the version's entry to `Wintor.Core/WhatsNew.cs` and its strings.
2. Run the tests (`dotnet test Wintor.sln -p:Platform=x64`) and the UI tests.
3. Build the upload, unsigned, for both architectures:
   ```powershell
   winapp package src\Wintor.App\Wintor.App.csproj -c Release --arch x64 --arch arm64 --no-sign --output Wintor.msixbundle
   ```
4. Optional check on a clean machine (Windows Sandbox): sign a copy with a test certificate
   (`winapp package … --generate-cert --install-cert`), install, add a legal torrent, quit from the tray, restart.
5. **Submission**: the product → Start submission.
   - Packages: upload `Wintor.msixbundle`.
   - Store listings → English: paste from [listing.md](listing.md), upload the screenshots it names.
   - Properties: category, privacy policy URL, support contact (all in listing.md).
   - Age ratings: answer the questionnaire as in listing.md.
   - Submission options → notes for certification and the `runFullTrust` justification from listing.md.
   - Pricing and availability: free, all markets.
6. **Submit for certification.** It usually takes from a few hours to three working days. Rejection reasons come
   by e-mail and in Partner Center; fix, raise the version, upload again.
7. After it is live: tag the commit (`git tag v<version>`), push the tag, and attach the same bundle to a GitHub
   release if you also offer it there.
