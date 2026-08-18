# 配布形態メモ

Norton などの AV 製品は、未署名の新規アプリに対してレピュテーションとヒューリスティックを強く使います。  
このアプリは次の要素を持つため、誤検知されやすい側です。

- グローバルキーフック
- Raw Input
- 最前面の透明クリック透過オーバーレイ
- スタートアップ登録機能

さらに、`.NET` の単一ファイル配布は、設定によってはネイティブ部品を実行時に展開します。  
そのため、検知を減らしたい場合は単一ファイルを既定にしない方が安全です。

## 推奨順

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

既定では `fdd` と `scd` を生成します。  
単一ファイルも作る場合は次です。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Distributions.ps1 -Profiles fdd,scd,single-file
```

## 出力先

- `dist\CrosshairOverlayTool-win-x64-fdd`
- `dist\CrosshairOverlayTool-win-x64-fdd.zip`
- `dist\CrosshairOverlayTool-win-x64-scd`
- `dist\CrosshairOverlayTool-win-x64-scd.zip`
- 任意で `dist\CrosshairOverlayTool-win-x64-singlefile`

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
