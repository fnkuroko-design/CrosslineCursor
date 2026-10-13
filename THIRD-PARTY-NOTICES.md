# 第三者コンポーネントとライセンス

CrosslineCursor 本体の MIT ライセンスは、第三者の著作権・ライセンスを置き換えません。

## .NET apphost

FDD 版はランタイム一式を同梱しませんが、EXE には .NET の起動用コード apphost が含まれます。

- 配布元: [.NET Runtime](https://github.com/dotnet/runtime)
- パッケージ: [Microsoft.NETCore.App.Host.win-x64](https://www.nuget.org/packages/Microsoft.NETCore.App.Host.win-x64)
- ライセンス: MIT。著作権者は .NET Foundation and Contributors。

公式 ZIP の `licenses` フォルダには、実際のビルドで使用したパッケージから次の文書を改変せずコピーします。

- `dotnet-apphost-LICENSE.txt`: 配布元の `LICENSE.TXT`
- `dotnet-apphost-THIRD-PARTY-NOTICES.txt`: 配布元の `THIRD-PARTY-NOTICES.TXT`
- `apphost-package.json`: 使用パッケージ名・版・ソースコミット・通知ファイルの SHA-256

第三者通知には .NET Runtime 全体に関する項目も含まれます。その全コンポーネントが本アプリに同梱されることを意味しません。必要な表示を誤って削除しないため、配布元の通知をそのまま保持します。

## 外部インストールするランタイム

.NET Desktop Runtime 10（Windows x64）は、利用者が [Microsoft の公式配布元](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) から別途取得・インストールしてください。その配布物には Microsoft が提示するライセンスが適用されます。本プロジェクトの MIT ライセンスでランタイムを再許諾するものではありません。

Windows の API とインストール済みフォントを利用しますが、Windows DLL やフォントファイルを同梱しません。トレイアイコンはコードで描画します。
