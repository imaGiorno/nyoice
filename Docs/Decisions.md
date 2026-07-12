# Decisions

## D-001: プロジェクト固有Assetを`Assets/_Project`へ集約する

- 日付: 2026-07-12
- 状態: 採用
- 決定: ゲーム固有のAssetとスクリプトを`Assets/_Project`以下へ置く。
- 理由: Unity標準Assetや将来導入する外部Assetとの境界を明確にするため。

## D-002: Scene生成をEditorメニューへ集約する

- 日付: 2026-07-12
- 状態: 採用
- 決定: `Nyoice/Setup Sprint 1`からTitleSceneとGameSceneを生成する。
- 理由: 初回セットアップの操作を減らし、Build Settingsの登録順を再現可能にするため。
- 安全策: 対象Sceneが1つでも存在する場合は処理開始前に中止し、既存作業を上書きしない。

## D-003: 初期タイトルUIはUnity標準uGUIを使う

- 日付: 2026-07-12
- 状態: Sprint 1暫定
- 決定: SetupスクリプトがCanvas、Text、透明Button、EventSystemを生成し、`TitleSceneController`へ開始イベントを接続する。
- 理由: Unity 6標準構成だけでクリック・タップ入力を提供し、IMGUI Moduleへの依存を避けるため。
- 見直し条件: 本番向けレイアウト、フォント、アニメーション、アクセシビリティ対応を実装するとき。

## D-004: GameStageはEditor生成する配置基盤とする

- 日付: 2026-07-12
- 状態: 採用
- 決定: `Nyoice/Setup Game Stage`がUnity標準PrimitiveでGameStageを生成する。
- 理由: 外部素材なしで、NPC、便器制御、待機列、経路ロジックが参照できる安定したTransform構成を用意するため。
- 境界: EditorコードはScene構築だけを担当し、RuntimeコードにはNyoiceラインの意味を示すマーカーのみを置く。
- 安全策: GameSceneに`GameStage`が存在する場合は生成せず警告する。
