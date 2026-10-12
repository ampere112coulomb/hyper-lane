# Hyper Lane iOS installation preview

This is a native visual-port preview, not a verified finished Android-equivalent port.
No songs, user scores, settings, Apple credentials, or signing certificates are included.

## Cloud build

1. Decide whether the source repository may be public. Public means anyone can read these sources and resources. Do not upload without that approval.
2. Standard GitHub macOS runners are free for public repositories. For a private repository, check remaining Actions minutes and a zero spending budget before running. Do not enable paid overages.
3. Upload this prepared directory to the approved repository. Run Actions → Build iOS installation preview → Run workflow.
4. Download the artifact `HyperLane-iOS-Preview-unsigned` after success and extract the IPA.
5. The IPA is unsigned and cannot be installed directly. Re-sign locally with the tester's own Apple account using AltStore Classic/AltServer. Never send account passwords to the developer or put them in GitHub secrets for this workflow.
6. Import a chart ZIP from Files. OGG Vorbis is converted to cached M4A on first use. The native skin renderer and menus are implemented, but screenshot/device verification and the non-brightness filter compositor remain unfinished; see ios/README.md.
7. Refresh through AltStore before the free signature expires. Do not uninstall to refresh.

The workflow runs only manually, has read-only repository permissions, retains artifacts for three days, and never uploads to App Store Connect or creates a public release.

Device and simulator compilation have succeeded. Compilation alone does not establish successful startup or visual equivalence. The simulator check must produce each managed screen-ready marker and its screenshot. The disposable ARM simulator app gets an ad-hoc local signature; the device IPA stays unsigned for the tester's own AltServer signature.

## References

- https://docs.github.com/en/actions/concepts/billing-and-usage
- https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties
- https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows
