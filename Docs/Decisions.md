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

## D-005: 待機列はNyoiceラインの入口側へ置く

- 日付: 2026-07-12
- 状態: 採用
- 決定: Nyoiceラインを`x = 6.0`、ライン直前の`NyoiceApproachPoint`を`x = 6.2`、`Queue01`を`x = 6.5`に置き、Queue番号順に入口方向へ並べる。
- 理由: 待機中に目的地確定ラインを越えず、先頭NPCだけが便器選択を受け付けながらライン直前へ進めるようにするため。
- Spawn: `SpawnPoint`は`x = 10.1`とし、`Queue08`よりさらに入口側へ置く。

## D-006: NPC待機列は固定8枠と内部リストで管理する

- 日付: 2026-07-12
- 状態: 採用
- 決定: 画面上の`Queue01`〜`Queue08`を`QueueSlot`として管理し、満員時は`List<NPCController>`へ生成順に保持する。
- 進入: 新規NPCは空いている最も大きいQueue番号へ入り、最初の空きが発生するたびに1つ前へ進む。
- 先頭: `Queue01`到着後は`DecisionPoint`へ移動し、便器選択待ちとして停止する。
- 移動: NavMeshやTween Packageを使わず、`Vector3.MoveTowards`で移動する。
- Prefab: Runtimeコンポーネントを持つルートと見た目の`Visual`子を分け、後から素材だけを差し替え可能にする。

## D-007: 内部待機NPCは生成済みのまま非表示にする

- 日付: 2026-07-12
- 状態: 採用
- 決定: 画面上のNPCをDecisionPointを含め最大8人とし、9人目以降はRendererとColliderを無効化して内部待機リストへ保持する。
- 表示責務: 全子孫のRendererとColliderの切り替えは`NPCController`だけが担当する。
- 復帰: 表示枠とQueueSlotが空いた時点で、内部待機NPCを表示状態へ戻してSpawnPointからQueueへ移動させる。
- 非採用: Sprint 3 MVPではObject Poolを導入せず、生成済みNPCを再利用する。

## D-008: Queue参照はSetup保存とRuntime検証を併用する

- 日付: 2026-07-13
- 状態: 採用
- 決定: SetupがQueueSlot配列を明示的にDirty保存し、QueueManagerがAwake時に8枠とDecisionPointを検証する。
- 復旧: 参照が欠損していれば`GameStage/Queue`以下の固定名からQueue01〜Queue08を順番に再取得する。
- 失敗時: Queue構成を復旧できない場合はNPCSpawnerを停止し、NPCをSpawnPointへ生成し続けない。
- 理由: Sceneのシリアライズ参照欠損を、全NPCが内部待機扱いになる挙動へ暗黙変換しないため。
# Sprint 4 decisions

- A ticket is owned by an NPC rather than represented only by a counter. This prevents double acquisition and double release.
- Queue entry remains fixed at Queue08. The DecisionPoint occupant leaves only after ticket acquisition succeeds.
- The current urinal selection belongs exclusively to the one NPC occupying SelectionZone. It may change until that NPC crosses NyoiceLine.
- UrinalTicket limits total admission to eight, while QueueManager independently limits SelectionZone to one occupant.
- The eight-NPC display cap uses a unique set across SelectionZone, DecisionPoint, and QueueSlots so transition overlap cannot double-count an NPC.
- Selection input receives a two-second default pause at NyoiceApproachPoint so pointer and keyboard choices are observable before line crossing.
- The selected urinal uses an unlit yellow four-sided frame in front of the Body instead of relying on subtle emission.
- Urinal Body and Highlight placement uses local transforms only. Existing Highlights are deleted and rebuilt on every setup run so malformed scene children are repaired without replacing GameStage.
- Urination duration belongs to each NPC and defaults to six seconds after Sprint 5-3A timing validation. `ReadyToLeave` represents completed use without implying urinal release, Ticket return, or exit movement.
- Urinal release and Ticket return are deferred until movement from UsePoint to ExitStartPoint begins in Sprint 5-2.
- Automatic selection scans Urinal08 through Urinal01 and ignores Reserved or Occupied urinals.
- The destination is immutable after `UrinalController.Reserve()` succeeds.
- A kinematic Rigidbody is added to the NPC prefab so the NyoiceLine trigger receives collision events reliably.
- A generated in-memory audio clip provides a selection sound without an external asset.
- Package-free Editor validators are used because no test package may be added.

## Sprint 5-2 decisions

- Urinal release and UrinalTicket return occur together when movement from UsePoint to ExitStartPoint begins. ExitStartPoint arrival is intentionally too late for either release.
- `UrinalController.Release(NPCController)` returns a boolean and changes state only when the caller is the current reserver or user. Duplicate and foreign releases are harmless.
- The per-urinal `ExitStartPoint` reference belongs to `UrinalController`; the shared `ExitPoint` reference belongs to `QueueManager` and is copied to each enqueued NPC.
- `TicketReleased` remains the single signal that retries a FrontWaiting DecisionPoint NPC. No second queue progression path is introduced for exits.
- NPC destruction is scheduled only after the guarded `Leaving -> Finished` transition at ExitPoint. An Editor-only validation flag makes this observable without immediate Play Mode destruction.
- Object pooling remains deferred. Completed NPC GameObjects are destroyed after reaching ExitPoint.

## Sprint 5-3A decisions

- GameOver is an explicit shared state rather than `Time.timeScale = 0`. Each gameplay owner blocks new work, while every configured `NPCMovement` stops its active callback immediately through the GameOver event.
- Discomfort counts adjacent urinal pairs, not distinct NPCs. Three consecutive Occupied urinals therefore produce exactly two pairs.
- Occupied is the authoritative adjacency condition. This includes `UsingUrinal` and `ReadyToLeave`, while Reserved is excluded. Existing Sprint5-2 release timing removes a urinal from adjacency as soon as leaving begins.
- Discomfort is frame-rate independent: pair count is multiplied by the serialized per-pair rate and `Time.deltaTime`.
- GameOver is one-shot and clamps discomfort to 100 before notifying listeners. The UI receives both value and state events so `100 / 100` and `GAME OVER` remain synchronized.
- The status HUD uses standard uGUI and the built-in legacy runtime font. No external font, asset, package, IMGUI, or restart control is introduced.
- NPC movement defaults to 4.0 units per second and urination to six seconds. This creates a visible Occupied overlap without changing queue, line, urinal, MovePoint, or UsePoint coordinates.

## Sprint 5-3B decisions

- The guarded `Finished` transition retains a one-shot completion notification and increments the processed-NPC count. It does not award points while base points, award timing, calculation, and fractional rounding remain unconfirmed.
- Combo uses a fixed stage index for `×1.0`, `×1.5`, `×2.0`, `×2.5`, and `×3.0`. Each uninterrupted five-second no-adjacency interval advances one stage and never exceeds ×3.0.
- `DiscomfortManager` remains the adjacency authority. A positive adjacent-pair event immediately resets combo and elapsed time, while Reserved urinals remain excluded by the existing Occupied-pair rule.
- GameOver freezes score state, processed count, combo multiplier, and combo elapsed time.

## Sprint 5-4 decisions

- Each guarded NPC completion at Exit awards `Mathf.RoundToInt(100 × ComboMultiplier)` and emits the existing `ScoreChanged` event after the score is updated.
- The Sprint 5-3B combo stages, timing, cap, adjacency reset, Reserved behavior, and GameOver freeze remain unchanged.
