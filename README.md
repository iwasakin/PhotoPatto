# PhotoPatto

WPF画像プレゼンテーションアプリケーション for .NET 10

## 概要

PhotoPattoは、画像をサムネイル一覧で管理し、セカンダリモニターにフルスクリーン表示できるWPFアプリケーションです。プレゼンテーションや展示会での画像表示に最適です。

![PhotoPatto Screenshot](docs/screenshot.png)

## 主な機能

✅ **サムネイルグリッド表示** - ストリーミング読み込みで高速表示  
✅ **フルスクリーン出力** - セカンダリモニターへのスムーズなクロスフェード表示  
✅ **EXIF自動回転** - カメラの向きに応じて自動的に画像を回転  
✅ **マルチモニタ対応** - 出力先モニターの選択と即時切替  
✅ **ソート機能** - ファイル名/更新日付で昇順/降順ソート  
✅ **キーボード操作** - 左右矢印キーでの画像ナビゲーション  
✅ **回転機能** - 90度単位での手動回転（全ビューに即反映）  
✅ **設定の永続化** - 前回の状態を自動保存・復元  
✅ **モダンUI** - Material Design風のフラットデザイン

## システム要件

- **OS**: Windows 10 / Windows 11
- **フレームワーク**: .NET 10
- **開発環境**: Visual Studio 2022 or 2025

## インストール

### ビルド方法

```bash
# リポジトリをクローン
git clone https://github.com/iwasakin/PhotoPatto.git
cd PhotoPatto
dotnet build
cd PhotoPresenter

# Visual Studioで開く
start PhotoPresenter.sln

# または、コマンドラインでビルド
dotnet build
```

### 実行

```bash
dotnet run --project PhotoPresenter
```

または、Visual Studioで F5 キーを押してデバッグ実行

## 使い方

1. **フォルダ選択**: 「フォルダ選択」ボタンで画像フォルダを開く
2. **サムネイル選択**: 一覧から表示したい画像をクリック
3. **フルスクリーン表示**: 
   - 出力先モニターを選択
   - 「Show」トグルをONにする
4. **ナビゲーション**: 
   - Prev/Nextボタン または
   - 左右矢印キー（両ウィンドウで使用可能）
5. **回転**: 回転ボタン（↺/↻）で90度ずつ回転
6. **黒画面**: 「Black」トグルで一時的に黒画面を表示

## 対応画像形式

- JPEG (`.jpg`, `.jpeg`)
- PNG (`.png`)
- BMP (`.bmp`)

## プロジェクト構成

```
PhotoPresenter/
├── PhotoPresenter/
│   ├── App.xaml / App.xaml.cs          # アプリケーションエントリポイント
│   ├── MainWindow.xaml / .cs           # メインウィンドウ
│   ├── FullscreenWindow.xaml / .cs     # フルスクリーンウィンドウ
│   ├── Models/
│   │   └── ImageItem.cs                # 画像データモデル
│   └── Services/
│       ├── ImageLoader.cs              # 画像読み込みサービス
│       └── SettingsManager.cs          # 設定管理サービス
└── Docs/
    ├── PhotoPresenter.md               # 機能仕様書
    └── CHANGELOG.md                    # 変更履歴
```

## 技術スタック

- **フレームワーク**: WPF (.NET 10)
- **言語**: C# 14.0
- **UI**: XAML + Code-behind
- **非同期処理**: `async`/`await`, `IAsyncEnumerable`
- **画像処理**: `BitmapImage`, EXIF metadata
- **マルチモニタ**: Windows Forms interop (`Screen.AllScreens`)
- **設定永続化**: JSON serialization

## 技術的な特徴

### 高速サムネイル読み込み
```csharp
await foreach (var item in ImageLoader.LoadFromFolderStreamAsync(folderPath))
{
    _images.Add(item);  // 1枚ずつ表示
}
```

### EXIFメタデータ対応
カメラの回転情報（Orientation Tag 274）を自動的に読み取り、正しい向きで表示

### クロスフェードアニメーション
2つのImageコントロールを使用した500msのスムーズなトランジション

## 今後の予定

- [ ] 動画対応（MP4, MOV等）
- [ ] スライドショーモード
- [ ] お気に入りマーク機能
- [ ] フォルダ履歴
- [ ] ズーム機能

詳細は [CHANGELOG.md](Docs/CHANGELOG.md) を参照

## ライセンス

MIT License

## 貢献

プルリクエストを歓迎します！大きな変更の場合は、まずissueで議論してください。

## 作者

- 開発: [Your Name]
- バージョン: 1.0.0
- 更新日: 2025

## 関連リンク

- [ドキュメント](Docs/)
- [変更履歴](Docs/CHANGELOG.md)
- [Issue Tracker](https://github.com/YOUR_USERNAME/PhotoPresenter/issues)
