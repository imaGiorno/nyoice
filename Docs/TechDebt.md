# Technical Debt

## 現在の技術的負債

### タイトル画面のUI実装

- 現状: 依存を増やさないためIMGUIで描画している。
- リスク: 解像度別レイアウト、フォント、演出、アクセシビリティの調整に向かない。
- 対応案: UI仕様確定後にUI ToolkitまたはuGUIへ移行する。

### 自動テスト未整備

- 現状: Scene生成とBuild Settings登録のEditorテストがない。
- リスク: Unityバージョン更新時のEditor API差分を自動検知できない。
- 対応案: Unity Test Framework導入時に、既存Scene保護とScene登録順のテストを追加する。

### Unity Editor実機確認

- 現状: この初期実装ではUnity Editor上のコンパイルとPlay Mode動作を未確認。
- 対応案: Unity 6でセットアップメニューを実行し、Scene生成、遷移、Build Settingsを確認する。
