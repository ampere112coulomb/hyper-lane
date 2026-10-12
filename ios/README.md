# Hyper Lane iOS 見た目移植プレビュー

Androidと共通の判定・譜面処理を使う.NET for iOSプロジェクトです。公開承認済みのコードと既存ゲーム画像だけをGitHub ActionsのMacでビルドします。個人の楽曲、譜面、スコア、設定、Apple認証情報はアップロードしません。生成IPAは未署名で、AltServer等で使用者が署名します。App Storeへの提出は行いません。

## 構成

- `../shared/864beat.MobileCore.csproj`: Androidの譜面・判定・BPM/BS/NS・レイヤー・Large Notes/Hold・カメラ・判定線・ZIP・レート・リプレイ・タッチ範囲のソースを共有します。
- `IosGameStage.cs`, `IosVisuals.cs`, `IosExtraScreens.cs`: 1920×1080基準のタイトル、モード、選曲、ロード、リザルト、設定、Best30、練習モード。背景・ロゴ・ふすま・ランク画像を既存資産から読み込みます。
- `IosLaneRenderer.cs`: スキンアトラスとAndroidのカメラ計算を使うGLES描画。レーン、ノーツ、深度、判定線、キービーム、カバーを描画します。
- `IosDan.cs`, `IosReplay.cs`: 段位選択・連続プレイ・条件ゲージ・リザルトとリプレイの接続。
- `IosSettings.cs`: JSON設定保存と3枠のプリセット。
- `IosNoteSound.cs`: 単一AVAudioEngineと8個の再利用ノード。0.1.2でSE負荷の改善をiPad実機確認済みです。この方式を維持します。
- `IosAudioConverter.cs`: OGG Vorbisを初回のみM4Aへ変換しDocuments/audio-cacheへ保存。元音源は変更しません。

## Windowsで検証済み

共通ライブラリのビルドと`../apk/tests/TouchZoneChecks.csproj`の回帰テスト。判定、各種ギミックの計算、段位条件、練習モードの8拍準備、設定保存を確認します。これらはiOSの実際の画面・音・フレーム時間を保証するテストではありません。

Android APK本体のソース・配布ファイル・端末は、この準備作業では更新しません。テストだけが共通プロジェクトを参照するよう変更されています。

## Macで次に確認すること

1. Xcodeと、そのXcodeバージョンに対応する.NET 10 iOS workloadを用意する。
2. `dotnet build ios/864beat.iOS.csproj -f net10.0-ios -r iossimulator-arm64` を実行し、APIのバインディングやInfo.plistを確認する。Intel MacではRIDを `iossimulator-x64` に変更する。
3. iPadシミュレーターで起動、横画面、ZIPピッカー、対応音源再生、同時押し、ホールド、オートSE、カメラ-90度を検証する。
4. 実機検証時にApple署名を設定する。このリポジトリに証明書・秘密鍵・アカウント情報を入れない。

`Build-iOS-on-Mac.sh` はローカルビルドのみを行い、クラウド契約やApp Storeへの公開を行いません。

## 完全移植までの未完了項目

- ARM iPadシミュレーターで正常起動し、タイトル・モード・選曲・設定・リザルトの描画を確認済み。3Dレーンの描画検証と、その後のiPad実機でのレイアウト、アイコン、動画、負荷の確認は未完了。
- 明るさ以外のフィルター（MONO / INVERT / MOSAIC / SCANLINE / BLOOM / GLITCH）の描画。データ・時間計算は共通ですが、現在のiOSレンダラーでは未接続です。
- 動画背景と音源の実機同期、復帰時の挙動、メモリ・フレーム時間の検証。
- フォント、粒子、アニメーションの実画面比較による最終調整。現段階で「完全に同一」とは扱いません。

スマホ版の「プレイを動画化」は追加しません。

## 楽曲の取り込み

画面の「楽曲ZIPを取り込む」で「ファイル」からZIPを選択。展開先はアプリのDocuments/songです。Documentsのファイル共有も有効にしています。ZIP展開の1GiB制限とフォルダー外アクセス防止はAndroidと同じ処理です。

## 公式資料

- [.NETのターゲットフレームワーク](https://learn.microsoft.com/dotnet/standard/frameworks)
- [iOS開発に必要な環境](https://learn.microsoft.com/dotnet/maui/get-started/installation?tabs=visual-studio-code)
