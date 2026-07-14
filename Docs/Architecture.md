# Architecture

## 目的

Sprint 1では、タイトル画面とゲーム画面を起点に、今後のゲームロジックを安全に追加できる最小構成を用意する。

## ディレクトリ方針

- `Assets/_Project/Art`, `Audio`, `Materials`, `Sprites`: 表示・音声素材
- `Assets/_Project/Prefabs`: 再利用するGameObject
- `Assets/_Project/Scenes`: `TitleScene`と`GameScene`
- `Assets/_Project/ScriptableObjects`: 調整値や静的データ
- `Assets/_Project/Scripts/Core`: ゲーム進行とドメインルール
- `Assets/_Project/Scripts/Managers`: Scene内のシステム調停
- `Assets/_Project/Scripts/NPC`: NPCの状態・移動・利用処理
- `Assets/_Project/Scripts/Toilet`: 小便器の状態と割り当て
- `Assets/_Project/Scripts/UI`: 画面表示と入力受付
- `Assets/_Project/Scripts/Utility`: 汎用的な小さな補助処理
- `Assets/_Project/Editor`: Unity Editor専用ツール

## Scene構成

### TitleScene

Unity 6同梱の標準uGUI（Canvas、Text、透明Button）でタイトルと開始案内を表示する。Buttonから`TitleSceneController`を呼び出し、画面クリックまたはタップで`GameScene`へ遷移する。外部アセットやUnity外の追加Packageには依存しない。

### GameScene

`GameStage`をルートとして、8基の便器、ついたて、右側入口、8人分の表示待機位置、Nyoiceライン、左側出口、各便器の移動用Waypointを保持する。Sprint 2では配置基盤のみを提供し、NPCやゲーム進行ロジックは持たない。

## セットアップ

Unityメニューの`Nyoice > Setup Sprint 1`がSceneを生成し、Build Settingsの先頭へ`TitleScene`、`GameScene`の順に登録する。既存の対象Sceneがある場合は上書きせず処理を中止する。

`Nyoice > Setup Game Stage`がGameSceneへステージ基盤を生成する。既に`GameStage`が存在する場合は、既存作業を保護するため生成を中止する。

## GameStage構成

- `Urinals`: `Urinal01`から`Urinal08`までの8基と番号表示
- `Partitions`: 各便器右側のついたて。`Partition08`はNyoiceラインの開始位置を示す
- `Entrance`: 右側入口と、待機列最後尾より入口側に置くNPC生成位置`SpawnPoint`
- `Queue`: ラインに最も近い`Queue01`から入口方向へ並ぶ`Queue08`、`DecisionPoint`、ライン直前の`NyoiceApproachPoint`
- `NyoiceLine`: NPCの便器目的地を確定する境界
- `Exit`: 左側出口と`ExitPoint`
- `Waypoints`: 各便器の`MovePoint`、`UsePoint`、`ExitStartPoint`

待機中のNPCはNyoiceラインの入口側（右側）に留まる。Sprint 3では先頭NPCが`Queue01`から`DecisionPoint`へ移動して停止し、便器選択待ちになる。後続Sprintでは`DecisionPoint`から`NyoiceApproachPoint`へ進み、ラインを右から左へ越えた時点で便器の目的地を確定する。

空間上の進行順は次のとおりとする。

`Queue01 → DecisionPoint → NyoiceApproachPoint → NyoiceLine → MovePoint → UsePoint → ExitStartPoint`

## NPC待機システム

- `NPCSpawner`: `SpawnPoint`で3秒ごとにNPCを生成する
- `QueueManager`: 8個のQueueSlot、画面上の最大8人、先頭への前進、DecisionPoint占有、内部待機リストを管理する
- `QueueSlot`: Queue番号と現在のNPC占有状態を保持する
- `NPCController`: Queue移動中、Queue待機中、DecisionPoint待機中の状態を調停する
- `NPCMovement`: `Vector3.MoveTowards`で速度2.5の直線移動を行う

`Setup Game Stage`は`Assets/_Project/Prefabs/NPC.prefab`を生成する。PrefabはRuntimeロジックを持つルートと、後で差し替え可能な`Visual`子オブジェクトを分離する。Scene上には`GameSystems/NPCSpawner`と`GameSystems/QueueManager`を生成する。

画面上に表示するNPCは、QueueSlotとDecisionPointを合わせて最大8人とする。9人目以降も3秒ごとに生成するが、`NPCController`が自身と全子孫のRendererおよびColliderを無効化して内部待機する。表示枠とQueueSlotに空きができると、内部待機リストの先頭NPCがRendererとColliderを再有効化し、SpawnPointから割り当て先QueueSlotへ移動する。

QueueManagerはSetup時に保存されたQueueSlot配列を使用する。Scene参照が未保存・欠損している場合に備え、Awake時に`GameStage/Queue/Queue01`〜`Queue08`と`DecisionPoint`を名前順で再取得する。NPCSpawnerもQueueManagerとSpawnPointを実行時に検証し、初期化できない場合は生成を停止して明示的なエラーを出す。

## 依存方向

UI、NPC、ToiletはCoreで定義するゲーム状態を参照し、ManagersがScene上の進行を調停する方針とする。Sprint 1では将来の抽象化を先回りせず、必要になった責務だけを分離する。
# Sprint 4 urinal flow

Sprint 4 adds a state-driven path from the queue front to a urinal.

- `QueueManager` keeps the Sprint 3 queue compact and releases the DecisionPoint only after a ticket is acquired.
- `UrinalTicketManager` owns eight NPC-specific tickets and rejects duplicate acquisition or release.
- `UrinalManager` owns the current player selection, keyboard and pointer input, right-first fallback selection, and reservation confirmation.
- `UrinalController` owns one urinal's number, state, waypoints, renderer, and highlight.
- `NPCController` owns the explicit `NPCState` transition from `Queue` through `UsingUrinal`.
- `NyoiceLine` is the trigger boundary that confirms the selected urinal.
- `QueueManager` serializes access to the SelectionZone. Only its occupant owns the active `UrinalManager` selection session.

The runtime flow is:

`Queue -> FrontWaiting -> ApproachingLine -> SelectingUrinal -> CrossingLine -> WalkingToUrinal -> UsingUrinal`

Only one NPC may be in `ApproachingLine` or `CrossingLine` as the SelectionZone occupant. The zone is released after the urinal reservation is confirmed at NyoiceLine, while the UrinalTicket remains held until a later exit sprint.

The visible waiting limit counts unique NPC references across SelectionZone, DecisionPoint, and all QueueSlots. Internal waiters remain hidden until this combined count is below eight.

At NyoiceApproachPoint, the SelectionZone occupant enters `SelectingUrinal` and remains stationary for a configurable two-second default window. Pointer, touch, and arrow input remain active throughout this wait. The yellow four-sided highlight frame is placed toward the camera in front of the urinal Body.

Each urinal Body is fixed at local `(0, 0, 0)` beneath its `UrinalXX` root. Setup deletes and rebuilds that urinal's Highlight as a direct child using Body-local coordinates, preventing all frames from collapsing to the world origin.

Sprint 5-1 extends the NPC flow to `WalkingToUrinal -> UsingUrinal -> ReadyToLeave`. `NPCController` owns a serialized three-second urination Coroutine. Completion changes only the NPC state: the urinal remains Occupied, the NPC remains at UsePoint, and its UrinalTicket remains held.

Tickets are released through `NPCController.ReleaseUrinalTicket()`. Sprint 4 exposes this entry point but does not start the exit flow.

## Sprint 5-2 exit flow

Sprint 5-2 completes the first reusable NPC lifecycle:

`UsingUrinal -> ReadyToLeave -> Leaving -> Finished`

`NPCController` starts leaving once urination completes and all exit references are valid. At the transition from `ReadyToLeave` to `Leaving`, it releases the occupied `UrinalController` and the NPC-owned ticket before moving. The route is the selected urinal's `ExitStartPoint`, followed by the shared `GameStage/Exit/ExitPoint`. The NPC is marked `Finished` and destroyed after reaching the shared exit.

Each `UrinalController` stores its own `ExitStartPoint`. `QueueManager` stores the shared `ExitPoint` and configures every newly enqueued NPC with it. Setup repairs both references on existing GameStages. The existing `TicketReleased` subscription immediately retries the DecisionPoint occupant, so a returned ticket can advance the next NPC into SelectionZone in the same frame.

Leaving uses guarded one-shot transitions. Urinal release, ticket release, ExitStart arrival, ExitPoint arrival, and destruction scheduling each ignore duplicate notifications. Disabled or destroyed NPCs clear their movement callback and active Coroutines.
