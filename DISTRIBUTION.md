# 配布形態メモ

Norton などの AV 製品は、未署名の新規アプリに対してレピュテーションとヒューリスティックを強く使います。  
このアプリは次の要素を持つため、誤検知されやすい側です。

- グローバルキーフック
- Raw Input
- 最前面の透明クリック透過オーバーレイ
- スタートアップ登録機能

さらに、`.NET` の単一ファイル配布は、設定によってはネイティブ部品を実行時に展開します。  
そのため、検知を減らしたい場合は単一ファイルを既定にしない方が安全です。

## 標準の配布形式

`fdd`（Framework-Dependent Deployment）を標準の配布形式とする。配布先には `.NET Desktop Runtime 8` が必要だが、ライブラリを同梱せず配布サイズを小さくでき、未署名アプリでの AV 誤検知を抑えやすい。

`scd` と `single-file` は必要になった場合にだけ明示指定して生成する。

## 形式の比較

1. `fdd`
   - `Framework-Dependent Deployment`
   - 検知を減らしたい場合の第一候補
   - 配布サイズが小さい
   - 配布先に `.NET Desktop Runtime 8` が必要
2. `scd`
   - `Self-Contained Deployment`
   - ランタイム不要
   - 単一ファイルではなく複数ファイルで配る
   - `single-file` よりは検知面で有利になりやすい
3. `single-file`
   - 利便性は高い
   - 自己展開を伴うため検知を受けやすい
   - AV 誤検知が問題になる配布では非推奨

## 配布コマンド

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Distributions.ps1
```

既定では `fdd` だけを生成します。
別形式も作る場合は、プロファイルを明示します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Distributions.ps1 -Profiles fdd,scd,single-file
```

## バージョン管理

- 正式なバージョン番号の唯一の管理元は `CrosshairOverlayTool.csproj` の `<Version>` である。この値からアセンブリ、ファイル、表示用のバージョン情報を生成する。
- 現在のリリースは `v1.0.1`。アプリのトレイメニューと設定画面に表示する。
- リリース前に `<Version>` を `MAJOR.MINOR.PATCH` 形式で更新する。破壊的変更は `MAJOR`、機能追加は `MINOR`、互換性を保つ修正は `PATCH` を上げる。
- リリース内容をコミットした後、同じ番号の注釈付き Git タグ（例: `v1.0.0`）を付与する。
- 配布スクリプトはこの値を読み取り、`v<Version>` を含む出力フォルダと ZIP を作成する。

## 出力先

- `dist\CrosshairOverlayTool-v1.0.1-win-x64-fdd`
- `dist\CrosshairOverlayTool-v1.0.1-win-x64-fdd.zip`
- 明示指定した場合は `scd` または `singlefile` の同名バリエーション

## 追加で効く対策

- Authenticode でコード署名する
- Norton / Symantec に誤検知申請する
- バージョン情報、製品名、会社名を PE に埋める
- まずは `fdd` 版で社内・知人配布し、問題がなければ `scd` に広げる

## 参考

- .NET single-file deployment  
  https://learn.microsoft.com/dotnet/core/deploying/single-file/overview
- .NET publishing overview  
  https://learn.microsoft.com/dotnet/core/deploying/
- Broadcom: Signed .NET apps may still need whitelist / false positive review  
  https://knowledge.broadcom.com/external/article/169441/signed-net-application-is-detected-as-fa.html
- Broadcom: Submit a false positive sample  
  https://knowledge.broadcom.com/external/article/178170/submit-false-positives-detected-by-endpo.html
