# Hyper Lane iOS installation preview

This is an installation/signing experiment, not the finished Android-equivalent port.
No songs, user scores, settings, Apple credentials, or signing certificates are included.

## Cloud build

1. Decide whether the source repository may be public. Public means anyone can read these sources and resources. Do not upload without that approval.
2. Standard GitHub macOS runners are free for public repositories. For a private repository, check remaining Actions minutes and a zero spending budget before running. Do not enable paid overages.
3. Upload this prepared directory to the approved repository. Run Actions → Build iOS installation preview → Run workflow.
4. Download the artifact `HyperLane-iOS-Preview-unsigned` after success and extract the IPA.
5. The IPA is unsigned and cannot be installed directly. Re-sign locally with the tester's own Apple account using AltStore Classic/AltServer. Never send account passwords to the developer or put them in GitHub secrets for this workflow.
6. OGG Vorbis is decoded in blocks and converted to M4A on first play. Already-imported songs also use this conversion; originals and charts remain unchanged. Converted audio is cached. Native readable formats play directly; unreadable formats are attempted through the iOS M4A exporter. Ogg Opus and arbitrary unsupported codecs are not covered by the Vorbis decoder. The final Android-equivalent renderer/UI is not implemented.
7. Refresh through AltStore before the free signature expires. Do not uninstall to refresh.

The workflow runs only manually, has read-only repository permissions, retains artifacts for three days, and never uploads to App Store Connect or creates a public release.

The 0.1.0 preview compiled successfully on macOS with Xcode 26.0.1 and .NET/iOS workload 10.0.100. The workflow pins that combination. 0.1.1 adds audio conversion, with mono/stereo decoder tests; M4A export and playback still require iPad verification.

## References

- https://docs.github.com/en/actions/concepts/billing-and-usage
- https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties
- https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows
