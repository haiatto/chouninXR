# 町人 XR（chouninXR）

現実の街に VRM の町人（NPC）を立たせ、近づくと話しかけてくる AR / XR アプリです。
Google の Geospatial API（VPS）で緯度・経度・高度に町人を配置します。

- 主な対象：**XREAL AURA（Android XR）**
- もう一つの対象：Android スマホ（ARCore）
- エンジン：Unity 6.6

各デバイスで最高の体験を優先し、プラットフォームごとに個別に実装します（汎用化のために機能を削りません）。

## 状態

設計段階です。設計の全体像は [docs/DESIGN.md](docs/DESIGN.md) を参照してください。

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
