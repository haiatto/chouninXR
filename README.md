# 町人 XR（chouninXR）

現実の街に VRM の町人（NPC）を立たせ、近づくと話しかけてくる AR / XR アプリです。
Google の Geospatial API（VPS）で緯度・経度・高度に町人を配置します。

- 主な対象：**XREAL AURA（Android XR）**
- もう一つの対象：Android スマホ（ARCore）
- エンジン：Unity 6.6

各デバイスで最高の体験を優先し、プラットフォームごとに個別に実装します（汎用化のために機能を削りません）。

## 状態

設計段階です。設計の全体像は [docs/DESIGN.md](docs/DESIGN.md) を参照してください。

## 構成

| フォルダ | 中身 |
|---|---|
| `ChouninXR/` | Unity プロジェクト（Unity 6000.6.7f1） |
| `ChouninXR/Assets/ChouninXR/Core` | 共通コア：町人データ、村の読み込み、セリフ進行、座標計算 |
| `ChouninXR/Assets/ChouninXR/Runtime` | 町人の表示（VRM 読み込み、セリフウィンドウ、出現管理） |
| `ChouninXR/Assets/ChouninXR/Platform/EditorSim` | Unity エディタ上で偽の位置情報で動かす |
| `ChouninXR/Assets/ChouninXR/Platform/AndroidXR` | XREAL AURA 用（Android XR Geospatial、実機未検証） |
| `villages/` | 村データ（町人の JSON と VRM） |

## エディタで動かす

1. Unity Hub で `ChouninXR/` を開く（Unity 6000.6.7f1）
2. `Assets/ChouninXR/Scenes/EditorSim.unity` を開いて再生
3. WASD で歩き、町人に近づくとセリフが出る。Space / クリックで送る

町人の姿は `villages/sample/chounin/<id>/model.vrm` に VRM を置くと反映されます（無ければ仮の体）。
VRM ファイルは配布条件がモデルごとに違うため、リポジトリにはコミットしない設定にしています。

## 初期の機能

- Geospatial API による位置合わせ
- VRM の町人を配置
- 近づくとセリフウィンドウで話す
- 町人の配置・セリフを作るエディタ（現地で配置 / 地図から配置）

## 拡張予定

- 時刻と天気から太陽の位置を計算し、町人に正しい影を落とす
- ローカル LLM（Gemma など）でセリフを生成し、音声認識で会話する
- GitHub リポジトリで町人データを配布・管理する

## API キーについて

Geospatial API には Google Cloud の API キーが必要です。キーはこのリポジトリにコミットしないでください。

## ライセンス

[MIT](LICENSE)
