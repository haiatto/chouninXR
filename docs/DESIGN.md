# 町人 XR（chouninXR）設計メモ

## 0. 旧アプリから読み取れたこと（2022年5月の動画 IMG_4098 / IMG_4123）

- ARCore Geospatial API の Unity サンプル（ARCore Extensions）をもとにしている
  - 画面上部のデバッグ表示（Latitude/Longitude、Horizontal Accuracy、Heading など）
  - 「18 anchor(s) set from history.」「Localization completed」などはサンプル由来の表示
- VRM の町人を緯度・経度・高度で配置している（駅の改札前の男性、階段のくノ一）
- 近づくと頭上に白いセリフウィンドウが出る（「パンダにあいたいかー！！！…」「くノ一階段」）
- UI ボタン：「でばっぐ」「村人パネル」「村人リスト」→ 当時から簡易エディタ／一覧があった

## 1. ターゲットと基盤

| 項目 | 方針 |
|---|---|
| Unity | **6000.6.7f1（Unity 6.6）**。XREAL AURA の公式サポートは Unity 6.5 以降。6000.3 は使わない |
| XREAL AURA | Android XR（OpenXR）＋ **Android XR Extensions for Unity**。Geospatial は `Android XR: Geospatial`（XRGeospatialManager）、アンカーは Shareable Anchors 拡張 |
| Android スマホ | AR Foundation ＋ **ARCore Extensions**（Geospatial） |
| ビルド | Unity の Build Profiles で「Phone」「AndroidXR」の2系統（XR プラグインが違うため APK は分ける前提） |
| 認証 | Geospatial は Google Cloud の API キー（ARCore API 有効化）。キーはリポジトリに入れない |

### プラットフォームの扱い（方針）

**各デバイスで最高の体験を優先する。汎用化のために機能を捨てない。**

- 共通にするのは「データ（村・町人 JSON、VRM）」と「町人の振る舞いの中身（セリフ進行、会話トリガーの判定など）」だけ
- AR まわり（位置合わせ、アンカー、遮蔽、平面・深度、ライティング推定）、入力、UI の見せ方はプラットフォームごとに個別実装してよい
  - 例：AURA は光学シースルー（黒は透明になる、視野が狭い）なのでセリフ UI の配色・配置からしてスマホと別物になる
  - 例：遮蔽（Depth / Occlusion）、Streetscape Geometry、Scene Semantics など片方にしかない機能は、ある側で全力で使う
- 共通の窓口（インターフェース）は「町人側が最低限必要とするもの」だけに絞る。窓口に載らない機能は各プラットフォームの実装が直接使う。窓口が機能の上限にならないようにする

### 入力・編集 UI

- **AURA**：ハンドトラッキングが基本（つまむ・つかんで町人を動かす、空間に浮かぶパネル）。コンピュートパックのトラックパッドなど AURA 固有の入力は、実機で調べながら AURA として最高の操作感を探る
- **スマホ**：2D の画面 UI（タップ、ドラッグ、ピンチ）
- 編集 UI は両者で別物として作る。共通なのは「何を編集するか」（町人データ）だけ

### Unity が未対応の機能

- Unity のパッケージで使えない Android XR / ARCore / Android の機能が必要になったら、Unity を通さない独自プラグイン（Kotlin/Java の Android ライブラリ＝AAR、必要なら C/C++ ネイティブ）を作って呼び出す
  - 候補例：Jetpack XR（ARCore for Jetpack XR）の新機能、Android の端末内音声認識、LiteRT-LM など
- プラグインは `Plugins/Android/` 配下に機能ごとに分け、C# 側に薄いラッパーを置く

```
共通コア（Chounin.Core）
  村・町人データ、セリフ進行、距離トリガー
  └─ 必要最小限の窓口：自分の位置・方位、町人の置き場所（Unity 座標）

プラットフォーム層（それぞれ独立、機能差OK）
  Chounin.AndroidXR … AURA。Android XR Geospatial、パックの入力、シースルー向け UI
  Chounin.Phone     … ARCore Extensions。Depth 遮蔽、Streetscape Geometry など
  Chounin.EditorSim … Unity エディタ上で偽の位置情報で確認するため
```

## 2. 初期スコープ（MVP）

1. **空間**：Geospatial で自己位置を確定 → 町人データの緯度経度高度にアンカーを張る
2. **町人（NPC）**：UniVRM（VRM 1.0 / 0.x 両対応のランタイム読み込み）、待機アニメ、プレイヤーの方を向く
3. **会話**：一定距離に入るとセリフウィンドウ（ワールド空間 UI）。複数行・ページ送り
   - スマホ：タップで送り / AURA：パック（コンピュートパック）のトラックパッドで送り
4. **エディタ**
   - 現地配置：その場でアンカーを置き、向き・高さを調整、VRM とセリフを割り当てて保存
   - 地図配置：地図上でピン → 緯度経度、高度は Terrain/Rooftop アンカーで解決。地図に Google マップは使わない（候補：地理院タイル、OpenStreetMap、PLATEAU）
   - VRM の取り込み（端末ストレージ / URL）と町人一覧の管理

## 3. データ形式（拡張「GitHub で配布」を最初から見据える）

町人1人 = 1フォルダ。最初はローカル保存、のちにそのまま GitHub リポジトリに置ける形にしておく。

```
village/                 # 「村」= 町人の集まり（リポジトリ1つ = 村1つ を想定）
  village.json           # 村の名前、作者、対象エリア、スキーマ版
  chounin/
    kunoichi-kaidan/
      chounin.json       # 下記
      model.vrm          # または外部 URL
      prompt.md          # 拡張：LLM 用の人格プロンプト
```

```json
{
  "schema": "chouninxr/chounin@1",
  "id": "kunoichi-kaidan",
  "name": "くノ一",
  "model": { "vrm": "model.vrm" },
  "placement": {
    "lat": 35.6747, "lng": 139.7390,
    "altitudeMode": "terrain",
    "altitude": 0.0,
    "heading": 180
  },
  "talk": {
    "triggerRadius": 3.0,
    "lines": ["くノ一階段へようこそ"]
  },
  "llm": { "enabled": false, "prompt": "prompt.md" }
}
```

- JSON Schema を `schema/` に置き、AI エージェントが生成・検証できるようにする（拡張で AGENTS.md も用意）

## 4. 拡張（MVP の後）

| 拡張 | 案 |
|---|---|
| 天候と影 | 緯度経度＋時刻から太陽高度・方位を計算（NOAA 式、自前実装で十分）→ Directional Light。天気は Open-Meteo（キー不要）で雲量→光の強さ・影の濃さ。シャドウは地面プレーン（Geospatial の Terrain / 平面検出）に落とす |
| ローカル LLM | **本命：LiteRT-LM ＋ Gemma 系**（独自 AAR プラグイン経由、GPU/NPU 活用）。AURA のパックで GPU/NPU が使えるか・応答速度は実機で計測。<br>調べる候補：OS 側の端末内 LLM（Android の AICore / Gemini Nano を ML Kit GenAI の Prompt API で使うもの）が AURA で使えるなら、無料・回数制限なしなので比較対象にする。使えない場合は Gemma のみ |
| 音声認識 | ① Android 標準 `SpeechRecognizer` の端末内認識（Android 12+、回数制限なし・無料）を第一候補 ② sherpa-onnx ＋ ReazonSpeech（日本語に強い、完全オフライン） ③ whisper.cpp |
| サーバー管理 | GitHub リポジトリ（村）を raw / GitHub Pages から取得。テンプレートリポジトリをフォーク／クローンして町人を追加 → アプリは村の URL を登録するだけ |

## 5. 進め方（案）

**AURA 版から先に作る**（実機は近日到着）。

1. Unity 6.6 プロジェクト作成、AndroidXR 用パッケージ導入（OpenXR / Android XR Extensions / UniVRM）
2. 実機到着までに：共通コア（町人データ、セリフ進行）と EditorSim で、エディタ上で町人が立ってしゃべる状態まで
3. AURA 実機で Geospatial → VRM 1体表示 → セリフウィンドウ（旧アプリの再現）
4. 町人データ（JSON）読み込み・複数配置
5. 現地配置エディタ（AURA のパック操作で）
6. 地図配置エディタ
7. Phone 版（ARCore 固有機能を活かした実装）
8. 拡張へ

## 参考
- Android XR for Unity: https://developer.android.com/develop/xr/unity
- ARCore Geospatial（Unity / AR Foundation, Android）: https://developers.google.com/ar/develop/unity-arf/geospatial/enable-android
- Unity の XREAL AURA サポート: https://unity.com/ja/blog/unity-android-xr-wired-glasses-support
- Android XR × Geospatial × Gemini の事例: https://developer.android.com/blog/posts/building-a-mixed-reality-tour-guide-with-android-xr-the-geospatial-api-and-gemini
