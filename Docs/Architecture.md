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

後続実装のための空Scene。ゲームループ、NPC、便器、UIは次の開発単位で追加する。

## セットアップ

Unityメニューの`Nyoice > Setup Sprint 1`がSceneを生成し、Build Settingsの先頭へ`TitleScene`、`GameScene`の順に登録する。既存の対象Sceneがある場合は上書きせず処理を中止する。

## 依存方向

UI、NPC、ToiletはCoreで定義するゲーム状態を参照し、ManagersがScene上の進行を調停する方針とする。Sprint 1では将来の抽象化を先回りせず、必要になった責務だけを分離する。
