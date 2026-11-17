# 町人 XR（chouninXR）

現実の街に VRM の町人（NPC）を立たせ、近づくと話しかけてくる AR / XR アプリです。
Google の Geospatial API（VPS）で緯度・経度・高度に町人を配置します。

- 主な対象：**XREAL AURA（Android XR）**
- もう一つの対象：Android スマホ（ARCore）
- エンジン：Unity 6.6

各デバイスで最高の体験を優先し、プラットフォームごとに個別に実装します（汎用化のために機能を削りません）。

## 状態

開発初期です。エディタ上のシミュレーションで町人が話すところまで動きます。AURA 版はビルドできますが実機は未検証です。
設計の全体像は [docs/DESIGN.md](docs/DESIGN.md) を参照してください。

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
4. Tab で配置の編集：クリックで持ち上げ／置く、N で追加、Q/E で回転、Delete で削除（村フォルダの `.trash` へ移動）、Esc で取り消し

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

## AURA（Android XR）版をビルドする

1. Unity のビルド先を Android に切り替える
2. メニュー **ChouninXR > AURA > Run All**（OpenXR の機能、Player 設定、`Aura.unity` シーンを作る）
3. メニュー **ChouninXR > AURA > APK ビルド**（`ChouninXR/Builds/Aura/` に出力）

`villages/` の村データはビルド時に自動でアプリへ同梱され、初回起動時に端末へ展開されます。

AURA での配置編集（仮の操作。実機で見直します）：左手ピンチ長押しで編集の開始／終了、右手ピンチで持ち上げ／置く、左手ピンチで追加（持っているときは 30° 回転）、右手長押しで取り消し、両手長押しで削除。

座標の基準（WGS84・楕円体高・真北）は [docs/COORDINATES.md](docs/COORDINATES.md) を参照してください。

## Google Cloud の設定（Keyless 認証）

Geospatial API は Google のサーバー（VPS）を使うため、Google Cloud にアプリを登録します。
API キーは使わず **Keyless 認証**（アプリの署名で認証する方式）にしているので、キーをアプリやリポジトリに入れる必要はありません。
ARCore API は 2026-11 時点で利用量の上限（クォータ）のみで、課金はありません。

1. [Google Cloud Console](https://console.cloud.google.com/) でプロジェクトを作る
2. **ARCore API** を有効にする
3. 「認証情報」で **OAuth クライアント ID（種類：Android）** を作る
   - パッケージ名：`io.github.haiatto.chouninxr`（fork した場合は自分のものに変える）
   - SHA-1：ビルドに使う keystore のもの。Unity の既定（デバッグ用）なら次で確認できる

     ```
     keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android
     ```
4. OAuth 同意画面の設定を求められたら、最低限の内容で作る

デバッグ用と本番用で keystore を分ける場合は、それぞれの SHA-1 を登録します。

## ライセンス

[MIT](LICENSE)
