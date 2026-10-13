# Hyper Lane Editor for iOS

Native WKWebView host for the same editor.html and touch UI used by Android Editor 0.1.9.
The app identifier is separate from the game; installing it does not replace game data.

Includes note placement, selection, long-press movement, pinch zoom, undo, copy/paste,
dedicated gimmick lanes, difficulty constants/display labels, settings and local test play.
Test play uses the editor's rendering and scheduled Web Audio with Android's seven
filter shaders, brightness and background-only/full-screen targeting.

Open .2d using Files. If sibling music cannot be accessed, grant its folder access when
prompted or choose music from the menu. OGG is converted locally to cached M4A;
no user music is uploaded. Existing .2d files are overwritten only on explicit Save.
New files use the native Files export picker. Opening a chart disables automatic
backup/restore to match Android. Audio stops when the app leaves the foreground.

Unsigned IPA requires the user's own signing and iPad/iPhone testing. Mac CI build
does not prove device touch response, audio synchronization or file-provider behavior.
