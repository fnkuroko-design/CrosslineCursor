# 軸拘束 現行設計

ファイル名や JSON 名に残る `horizontal_lock` は既存設定との互換性のための内部名称であり、UI 上の名称は「軸拘束」とする。

## 構成

- `Program`: WinForms の起動と旧診断ログの削除を行う。
- `AppController`: 表示、拘束、設定再適用、トレイ操作を一元管理する。
- `InputManager`: 低レベルキーボードフック、`RegisterHotKey`、Raw Input によるグローバル入力を処理する。
- `HoldKeyTracker`: 通常キーの押下状態と、英数キーの lease 方式疑似ホールドを追跡する。
- `EisuHotkeyTracker`: 英数を含むショートカットを疑似ホールド単位で一回だけ発火させる。
- `OverlayManager`: 約 60 FPS の `GetCursorPos` ポーリング、仮想デスクトップ領域、オーバーレイ表示を担当する。表示中の各オーバーレイフォームは、同じタイマーで `HWND_TOPMOST` へ非アクティブのまま再配置し、後から表示された通常の最前面ウィンドウより前面に戻す。
- `OverlayForm`: 縦線・横線それぞれの透明、クリック透過、最前面フォームを提供する。
- `CursorConstraintManager`: 軸の自動判定と `ClipCursor`／`SetCursorPos` による拘束を担当する。
- `SettingsManager` と `AppSettings`: JSON 設定の保存と正規化を担当する。
- `AppVersion`: プロジェクトのアセンブリ情報から表示用バージョンを取得する。
- `TrayManager`: 状態表示、固定表示、軸拘束、設定、終了を提供する。

描画、入力、カーソル拘束の責務は分離し、`InputManager` にカーソル操作を持ち込まない。

## 状態計算

`AppController.RecomputeState()` が表示と拘束の最終判定を行う。

```text
hold_show = EnableHoldMode && HoldKeyPressed
toggle_show = EnableToggleMode && ToggleStateOn
should_show = hold_show || toggle_show

should_lock = EnableHorizontalLock
              && hold_show
              && !suppressed_until_hold_release
```

`Esc` による解除は `suppressed_until_hold_release` を設定する。ホールドキーを離すとこの抑止状態を解除し、次のホールド操作で通常どおり軸拘束できる。

設定の適用時は、オーバーレイ、拘束、入力の各マネージャーを新しい設定で作り直した後に旧インスタンスを破棄し、状態を再計算する。

## 軸の決定と拘束

`CursorConstraintManager` は有効化時にアンカー座標と仮想デスクトップ領域を取得し、16 ms 間隔でカーソル位置を確認する。

```text
dx = abs(cursor.x - anchor.x)
dy = abs(cursor.y - anchor.y)

max(dx, dy) < 6 なら決定待ちを継続
dx >= dy        なら水平拘束（Y を anchor.y に固定）
dy > dx         なら垂直拘束（X を anchor.x に固定）
```

軸を決定した直後に `SetCursorPos` でアンカー軸へ補正し、幅または高さ 1 px の `ClipCursor` 領域を適用する。解除時はタイマーを停止して `ClipCursor(IntPtr.Zero)` を呼び、全画面拘束を確実に解除する。

## 入力処理

- 通常のホールドキーは key down／key up を追跡する。
- 英数キーは key-up の欠落・繰り返し通知の両方に備え、キーリピートを lease 更新として扱う。フックと Raw Input の key-up／break では即時解除せず、リピート停止後の期限到達で解除する。
- フックと Raw Input の到着間隔は別々に計測する。同じ入力の二重通知から、短いリピート間隔を推定しない。各経路の最初の間隔は初回リピート待ちなので、速度推定から除外する。
- 初期期限は `KeyboardDelay`、継続期限の下限は `KeyboardSpeed` に基づく。速度のハードウェア差を考慮した待ち時間と 55 ms の余裕を確保し、既存期限を短縮しない。通常の速いリピートでは継続期限は 110 ms。実測した遅いリピートに応じて最大 1,000 ms まで延ばす。
- 英数と確認済みの非ゼロのスキャンコード・拡張属性と一致する `VK_PROCESSKEY`／`VK_CAPITAL` はホールドを更新できる。これらのキーだけでは英数ホールドを開始しない。
- トグルキーと拘束切替キーは `RegisterHotKey` を基本とし、低レベルフックと Raw Input で不足を補う。
- 英数ショートカットは `EisuHotkeyTracker` で一ホールド一回に制限する。期限が切れるまで押し直しや修飾キーの再押下も同一ホールドになる。通常キーの `RegisterHotKey` 処理は変更しない。
- ショートカットに限り、従来の修飾キー付き `VK_PROCESSKEY` 単独通知へのフォールバックを残す。開始後は別スキャンコードの IME 入力で期限を更新しない。`VK_PROCESSKEY` 単独では物理キーを完全には識別できない点は既存の制約である。
- `Esc` は拘束が有効な間だけ緊急解除イベントを発行する。

## v1.0.2: 押下中のちらつきの調査と修正

確認版 `v1.0.2-dev.1` に対して、ユーザーから「今のところは大丈夫そう」との確認と正式化の依頼を受け、2026-10-06 に `v1.0.2` へ移行した。正式化ではバージョンと文書のみ更新し、入力・描画・拘束の機能変更は行っていない。全環境での解消を確認したものではない。

### コードで確認できた問題

1. 旧 `HoldKeyTracker` は、英数の key-down を lease 方式で扱う一方、key-up／Raw Input の break を通常キーの経路で即時解除していた。押下中に down/up が繰り返される入力列を与えると、ホールド状態が毎回 true/false になった。
2. `AppController` はホールド解除で表示を消し、軸拘束も解除する。再押下で拘束アンカーと 6 px の方向判定を開始し直す。このため、描画のちらつきと軸拘束の不成立が同時に起きる。
3. フック・Raw Input 共通の時刻でリピート速度を推定し、20 ms より遅い二重通知をリピートと誤認し得た。さらに、110 ms などの短い期限へ既存期限を上書きしていた。
4. 旧版は `KeyboardDelay` のみ参照して `KeyboardSpeed` を参照せず、120 ms を超えるリピート間隔を推定対象外としていた。短い初期遅延と遅いリピート速度の組み合わせを十分に扱えていなかった。
5. 英数ショートカットの 150 ms デバウンスだけでは、長押しで表示切替や軸拘束 ON/OFF が繰り返し発火する。新方式ではホールドが終わるまで再発火しない。

英数ホールド初期余白は初回待ち時間だけの調整であり、1 の即時解除や 5 の再発火には効かない。ボタンを離した後にリピートが止まり、最後の lease の間だけ安定して表示・拘束されるという報告とも整合する。ただし、問題の出る実機でどの入力列が届いているかは未採取であり、環境側の原因まで確定したものではない。

### 別の可能性と対象範囲

- 描画合成や最前面ウィンドウ同士の競合は表示のちらつきを起こし得るが、それ自体は `HoldKeyPressed` や軸拘束を解除しない。今回の同時症状は入力状態の誤解除を優先して修正する。描画・DPI・Z オーダー実装は変更しない。
- フック／Raw Input の通知欠落、キーボードや IME・割り当てソフトによる仮想キー／スキャンコード差、リモート接続や OS 設定を超える入力遅延は環境差の候補。確認済みキーの IME 表現変更と通常の速度差は今回の対象とする。
- リピートを全く送らない環境や、期限を超える無通知を、疑似ホールドだけで物理的な解放と区別することはできない。期限は無限に延ばさず、入力が止まったら解除する。再発時は問題のキーボード・IME・アプリ・割り当て設定で入力列の追加確認が必要。
- 他アプリが `ClipCursor` を上書きする場合は拘束だけ失われ得るが、英数押下に同期した表示 ON/OFF を直接説明しない。外部アプリとの拘束競合を本修正で解消したとは扱わない。

### 回帰検証

```powershell
dotnet run --project .\tests\InputRegression\InputRegression.csproj -c Release
```

外部パッケージなしのコンソールテスト。実装ソースをリンクし、時刻・キーボード設定だけを注入する。グローバルフック、カーソル拘束、設定保存、GUI 操作は実行しない。

- v1.0.1 のソースでフック down/up、Raw Input make/break の 2 ケースが失敗し、通常修飾キーの 1 ケースは成功することを確認。
- 修正版は 20 項目を検証。遅延二重通知、入力経路の順序、片経路のみ、初期余白両端、全 128 組の Windows 遅延・速度設定、IME 表現変更、期限解除、ショートカット長押しを含む。
- ホールド中は通知が true の一回のみ、リピート停止後に false が一回だけ届くことを確認し、入力起因の拘束再初期化を防ぐ。
- 実際のちらつき・水平／垂直拘束・Esc の確認は問題の出る環境で行う。旧版を終了して開発版だけを起動し、英数長押し中の移動、解放、Ctrl+英数／Alt+英数の長押しを確認する。開発版と正式版は設定を共有するが、テスト自体は設定を書き換えない。

キーボード速度とハードウェア差の根拠: [Microsoft Learn: SystemInformation.KeyboardSpeed](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.systeminformation.keyboardspeed?view=netframework-4.8.1)。通常キーのリピート通知仕様: [Microsoft Learn: Keyboard Input](https://learn.microsoft.com/en-us/windows/win32/learnwin32/keyboard-input)。これらは Windows の一般仕様であり、今回の実機で反復 key-up が発生したことを証明する資料ではない。

## DPI と表示

プロジェクトは `ApplicationHighDpiMode=PerMonitorV2` を指定し、`OverlayForm` は `AutoScaleMode.None` を使用する。仮想デスクトップ全体の縦線・横線フォームを用いるため、複数モニターをまたいでカーソル位置に追従する。

この再配置はログオン、UAC、`Ctrl`+`Alt`+`Delete`、ロック画面など別のセキュアデスクトップには及ばない。また、システム整合性レベルで保護された UI、排他的フルスクリーン、または描画合成を経由しない表示では、通常の Win32 ウィンドウの Z オーダーを保証できない。

## 互換性の残置項目

`HorizontalLockTriggerMode`、`HorizontalLockHoldKey`、および `InputManager` の別ホールドキー対応は、既存 JSON 設定を読み込めるよう残している。`AppController` は現行仕様として常に同一ホールドキー方式を渡し、設定の正規化でもこの方式へ統一する。

これらを削除する場合は、既存の `settings.json` を読み込んだ際の移行方針を先に定義する。
