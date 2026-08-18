# 軸拘束 現行設計

ファイル名や JSON 名に残る `horizontal_lock` は既存設定との互換性のための内部名称であり、UI 上の名称は「軸拘束」とする。

## 構成

- `Program`: WinForms の起動と旧診断ログの削除を行う。
- `AppController`: 表示、拘束、設定再適用、トレイ操作を一元管理する。
- `InputManager`: 低レベルキーボードフック、`RegisterHotKey`、Raw Input によるグローバル入力を処理する。
- `HoldKeyTracker`: 通常キーの押下状態と、英数キーの lease 方式疑似ホールドを追跡する。
- `OverlayManager`: 約 60 FPS の `GetCursorPos` ポーリング、仮想デスクトップ領域、オーバーレイ表示を担当する。表示中の各オーバーレイフォームは、同じタイマーで `HWND_TOPMOST` と `HWND_TOP` へ非アクティブのまま再配置し、後から表示された通常の最前面ウィンドウより前面に戻す。
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
- 英数キーは key-up 欠落に備えて、キーリピートを lease 更新として扱う。
- トグルキーと拘束切替キーは `RegisterHotKey` を基本とし、低レベルフックと Raw Input で不足を補う。
- 同一スキャンコードのトグル操作は、150 ms 以内なら重複として無視する。
- `Esc` は拘束が有効な間だけ緊急解除イベントを発行する。

## DPI と表示

プロジェクトは `ApplicationHighDpiMode=PerMonitorV2` を指定し、`OverlayForm` は `AutoScaleMode.None` を使用する。仮想デスクトップ全体の縦線・横線フォームを用いるため、複数モニターをまたいでカーソル位置に追従する。

この再配置はログオン、UAC、`Ctrl`+`Alt`+`Delete`、ロック画面など別のセキュアデスクトップには及ばない。また、システム整合性レベルで保護された UI、排他的フルスクリーン、または描画合成を経由しない表示では、通常の Win32 ウィンドウの Z オーダーを保証できない。

## 互換性の残置項目

`HorizontalLockTriggerMode`、`HorizontalLockHoldKey`、および `InputManager` の別ホールドキー対応は、既存 JSON 設定を読み込めるよう残している。`AppController` は現行仕様として常に同一ホールドキー方式を渡し、設定の正規化でもこの方式へ統一する。

これらを削除する場合は、既存の `settings.json` を読み込んだ際の移行方針を先に定義する。
