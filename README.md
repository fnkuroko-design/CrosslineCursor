# CrosslineCursor クロスラインカーソル

Windows のマウスカーソル位置に追従する、透明・クリック透過のクロスライン表示ツールです。タスクトレイに常駐し、ホールド表示、固定表示、水平・垂直の軸拘束を切り替えられます。

## ダウンロードと必要環境

公式配布は **Windows x64 用 FDD 版のみ**です。.NET ランタイムは同梱しません。自己完結版・単一ファイル版は配布しません。

1. **Microsoft .NET Desktop Runtime 8 の x64 版（最新の 8.0.x）を別途インストール**してください。[Microsoft 公式ダウンロード](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) の「.NET Desktop Runtime」から取得します。
2. [Releases](https://github.com/fnkuroko-design/CrosslineCursor/releases/latest) から `CrosshairOverlayTool-v<バージョン>-win-x64-fdd.zip` を取得します。
3. ZIP をフォルダごと展開し、`CrosshairOverlayTool.exe` を起動します。EXE だけを移動しないでください。

通常の「.NET Runtime」だけでは WinForms 用コンポーネントが不足します。実行に SDK は不要です。ランタイムの自動ダウンロード・自動インストールは行いません。

更新時は旧版をトレイメニューから終了してから新しい版を起動してください。ライセンス文書を含む配布フォルダ一式を保持してください。

## 使い方

| 操作 | 既定のキー・方法 |
| --- | --- |
| 押している間だけ表示 | `LeftAlt` |
| 固定表示の ON/OFF | `Ctrl+Alt+Shift+Q`、またはトレイアイコンを左ダブルクリック |
| 軸拘束の ON/OFF | `Ctrl+Alt+Shift+L`、設定画面、またはトレイメニュー |
| 拘束の一時解除 | `Esc`。ホールド終了まで再拘束しません |
| 設定・終了 | トレイアイコンの右クリックメニュー |

表示条件は「ホールド表示または固定表示」です。軸拘束はホールド表示中だけ有効で、最初に 6 px 以上動かした方向から水平・垂直を選びます。固定表示だけのときは拘束しません。

設定ではホールドキー、切替ホットキー、線の太さ・色・不透明度、スタートアップなどを変更できます。

## 注意事項

- 英数キーはリピート停止から解放を推定します。離した後も表示・拘束が短時間残る場合があります。拘束だけをすぐ解除する場合は `Esc` を押してください。
- `LeftAlt` はアプリのメニュー操作と干渉する場合があります。
- スタートメニュー、タスクマネージャーの常時手前表示、UAC のセキュアデスクトップ、排他的フルスクリーンなどでは、最前面表示を保証しません。
- 未署名のアプリです。グローバル入力監視や最前面表示を使用するため、セキュリティ製品から警告を受ける場合があります。

設定は `%LocalAppData%\CrosshairOverlayTool\settings.json` に保存します。スタートアップを有効にすると、`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` の `CrosshairOverlayTool` に起動先を登録します。設定を版ごとに分離していないため、複数版の同時起動は避けてください。

## 開発と配布

Windows と .NET SDK 8 以降を使用します。

```powershell
dotnet build .\CrosshairOverlayTool.csproj -c Release
dotnet run --project .\tests\InputRegression\InputRegression.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Distributions.ps1
```

配布スクリプトは FDD のみを生成し、ライセンス・第三者通知・必要ランタイムの案内を ZIP に含めます。同じ版が存在する場合は上書きせず停止します。詳細は [DISTRIBUTION.md](DISTRIBUTION.md) を参照してください。

## ライセンス

アプリのソースコードは [MIT License](LICENSE) です。商用利用・改変・再配布が可能ですが、著作権表示と許諾文の保持が必要です。

FDD の EXE に含まれる .NET apphost などには、それぞれのライセンスが適用されます。[第三者ライセンスについて](THIRD-PARTY-NOTICES.md) を参照してください。公式配布を FDD に限定する方針は、MIT ライセンスによる第三者の利用許諾を追加で制限するものではありません。
