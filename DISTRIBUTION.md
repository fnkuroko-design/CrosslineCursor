# 配布方針

公式成果物は **FDD（Framework-Dependent Deployment）の Windows x64 版のみ**とします。.NET ランタイム、ランタイムのインストーラー、自動インストール機能を同梱しません。SCD・single-file は生成・公開しません。

## 必要環境

**Microsoft .NET Desktop Runtime 10 の x64 版（最新の 10.0.x）の外部インストールが必須**です。通常の .NET Runtime のみでは不足します。SDK は実行には不要です。.NET 8 のみが入っている環境では v1.0.4 以降は動作しません。

[Microsoft 公式ダウンロード](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) の「.NET Desktop Runtime」を案内します。

## 互換版の公開期間

`v1.0.3` は互換性用の FDD 版として引き続き公開します。この版には **.NET Desktop Runtime 8 x64（最新の 8.0.x）の外部インストールが必須**です。案内先は [.NET 8 の公式配布元](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) です。

`v1.0.4` の次回更新時に `v1.0.3` の公開を停止する予定です。現在の公開整備では `v1.0.3` の Release と添付ファイルを保持し、説明に互換版であることと必要ランタイムを明記します。

## 配布コマンド

Windows と .NET SDK 10 で実行します。`global.json` は安定版 10.0 系 SDK を選択します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Distributions.ps1
```

互換性のため `-Profiles fdd` も受け付けますが、その他の形式は拒否します。同名の出力が既にある場合は上書きせず停止します。

処理内容:

1. `--self-contained false`、`PublishSingleFile=false` で発行し、ランタイムを含まず、外部の .NET 10 を要求することを確認。
2. アプリの `LICENSE`、`README.md`、`THIRD-PARTY-NOTICES.md` を同梱。
3. MSBuild の `ResolvedAppHostPack` から実際の apphost を特定し、そのライセンス・第三者通知・出典情報を `licenses` に同梱。SDK 内蔵パックに通知がない場合は、同じ版の公式 NuGet パッケージから取得し、apphost 本体の SHA-256 一致を確認する。取得・照合できない場合は停止。
4. ZIP と SHA-256 チェックサムを生成。

出力例:

- `dist\CrosshairOverlayTool-v1.0.4-win-x64-fdd\`
- `dist\CrosshairOverlayTool-v1.0.4-win-x64-fdd.zip`
- `dist\CrosshairOverlayTool-v1.0.4-win-x64-fdd.zip.sha256`

EXE 単体では配布せず、DLL とライセンスを含む ZIP 一式を使用します。過去にローカルで作成した SCD 等は Git 管理外のまま保持し、公開しません。

## バージョンと GitHub Release

- バージョンの管理元は `CrosshairOverlayTool.csproj` の `<Version>` です。
- `v1.0.4` は .NET 10 移行版です。表示・入力・拘束の機能と設定形式を維持し、必要ランタイムを Desktop Runtime 10 x64 に変更しました。
- 既存の成果物は保持し、新しい版として作成します。
- 公開コミットを確定後にビルドし、製品バージョン内のコミット ID と一致することを確認します。検証後、同じコミットに注釈付きタグを付けます。
- GitHub Release には FDD ZIP とその SHA-256 のみを添付します。GitHub が自動生成するソースアーカイブはアプリの実行用成果物ではありません。
- 開発版は `MAJOR.MINOR.PATCH-dev.N` で正式版と区別します。

## 更新と検証

起動中の旧版を終了してから、新しいフォルダの EXE を起動します。設定とスタートアップ登録名は版をまたいで共有します。配布物生成だけではユーザー設定やスタートアップ登録を変更しません。

```powershell
dotnet run --project .\tests\InputRegression\InputRegression.csproj -c Release
Get-FileHash .\dist\CrosshairOverlayTool-v1.0.4-win-x64-fdd.zip -Algorithm SHA256
```

回帰テスト、ビルド、バージョン・FDD 内容・ライセンス同梱・ZIP の整合性を確認します。実機での入力や表示の確認とは区別します。

アプリは [MIT License](LICENSE) です。第三者の条件は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) と ZIP 内の `licenses` を参照してください。

未署名で、グローバル入力監視・最前面表示を利用するため、セキュリティ製品に警告される可能性があります。FDD にしても誤検知がなくなる保証はありません。
