# 座標の基準

位置の数字は、API やプラットフォームごとに基準（測地系・高さの基準・回転の軸）が違います。
混ぜると数 m〜数十 m ずれ、しかも気づきにくいので、町人XR では **保存する基準を 1 つに固定** し、
取り込むところで必ずその基準に直します。

## 町人XR の基準（chounin.json に保存する値）

| 項目 | 基準 | 単位 |
|---|---|---|
| `placement.lat` / `lng` | WGS84 の緯度・経度（EPSG:4326） | 度 |
| `placement.altitude`（`wgs84` / `surface`） | **WGS84 楕円体高**（海抜・標高ではない） | m |
| `placement.altitude`（`terrain` / `rooftop`） | 地面・屋上の面からの高さ | m |
| `placement.heading` | **真北** = 0、時計回り（磁北ではない） | 度 |
| `placement.placedWith` | 置いたときの取得元・精度・日時・高さを決めた面 | — |

コードでは `GeoPose`（[Core/GeoPose.cs](../ChouninXR/Assets/ChouninXR/Core/GeoPose.cs)）がこの基準を表します。

### 高さの持ち方

- 現地で直接置いた町人は `surface` です。高さは **その場で見つかった面（メッシュ → 平面 → 地面相当）** で決めます。
  地図の標高や Geospatial の高度は数 m ずれることがあるためです。
  `altitude` には置いたときの WGS84 楕円体高を「目安」として残し、面が見つかるまでの仮の高さに使います。
- `terrain` / `rooftop` は Geospatial の地形・屋上アンカーに任せます（地図配置向け）。
- `wgs84` は高さをそのまま信じる場合だけ使います。

## API・プラットフォームごとの基準

| どこから | 緯度経度 | 高さ | 向き・回転 |
|---|---|---|---|
| Android XR Geospatial（Google XR Extensions） | WGS84 | WGS84 楕円体高 | 回転は **EUS**（+X=東, +Y=上, +Z=南） |
| ARCore Geospatial（スマホ版で使う予定） | WGS84 | WGS84 楕円体高 | **API によって違う**：Unity の ARCore Extensions は `EunRotation`（EUN, +Z=北）、Java/Kotlin は `getEastUpSouthQuaternion()`（EUS, +Z=南）。スマホ版を作るときに確認する |
| Android `Location`（GPS） | WGS84 | `getAltitude()` は WGS84 楕円体高。`getMslAltitudeMeters()`（API 34+）は海抜 | `getBearing()` は真北基準 |
| Android の方位センサー（コンパス） | — | — | **磁北** 基準。真北にするには偏角を足す（日本は西偏 約 5〜10°） |
| 地理院タイル | JGD2011。タイルの並びは Web メルカトル（EPSG:3857, z/x/y） | 標高タイルは **標高**（ジオイド基準の海抜）。楕円体高 = 標高 + ジオイド高（東京で約 36〜37m） | — |
| OpenStreetMap | WGS84。タイルは Web メルカトル | 基本的に持たない | — |
| PLATEAU | JGD2011（CityGML は EPSG:6697 = 緯度経度 + 標高） | CityGML は標高。3D Tiles 版の高さの基準は使う前に確認する | — |
| Unity（EditorSim） | 原点 = `SimWorld.originLat/Lng` | 原点の地面 = `originAltitude`（WGS84 楕円体高の目安） | +X=東, +Y=上, **+Z=北** |
| Unity（AURA・実機のワールド座標） | セッション開始時の姿勢が原点。起動ごとに変わる | 同左 | 毎回違う。**保存してはいけない** |

### 注意

- **JGD2011 と WGS84**：日本では実用上同じとみなします。ただし JGD2011 は 2011 年時点に固定された座標で、
  WGS84（ITRF 系）は地殻変動に合わせて動くため、年数 cm ずつ差が広がります。数十 cm〜1m 級の差が問題になったら、ここを見直します。
- **標高と楕円体高**：日本の高さ基準は 2025 年 4 月に GNSS 測量とジオイドモデル（ジオイド2024）を使う方式へ改定されました。
  地図の標高を楕円体高に直すときは、使うジオイドモデルを明記します。
- **回転の軸**：Geospatial は EUS（+Z が南）、EditorSim は +Z が北です。方位 ↔ 回転の変換は
  `GeoMath.HeadingToRotationEus` / `HeadingToRotationNorthZ` に集め、ほかの場所で計算しません。
- **Unity のワールド座標は保存しない**：実機ではセッションごとに原点が変わります。保存するのは必ず `GeoPose`（緯度経度・楕円体高・方位）です。

## 新しい API・プラットフォームを足すとき

1. その API が返す緯度経度・高さ・回転の基準を公式ドキュメントで確認し、この表に足す
2. 取り込むところ（`IChouninPlacementWorld.TryGetGeoPose` など）で町人XR の基準に直す
3. `placedWith.source` に新しい取得元の名前を足す（`GeoSources`）
