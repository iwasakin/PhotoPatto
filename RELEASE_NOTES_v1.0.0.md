# PhotoPatto v1.0.0 - Initial Release

WPF画像プレゼンテーションアプリケーションの最初のリリースです。

## ✨ 主な機能

- 📁 フォルダから画像を一括読み込み（.jpg, .jpeg, .png, .bmp）
- 🖼️ サムネイル一覧表示と高速プレビュー
- 🖥️ マルチモニター対応のフルスクリーン出力
- 🎬 クロスフェード切り替えエフェクト（500ms）
- 📸 EXIF Orientation自動補正
- 🔄 画像回転機能（90°単位）
- ⌨️ キーボードナビゲーション（左右矢印キー）
- 📊 ソート機能（ファイル名/更新日付、昇順/降順）
- 🎨 モダンなフラットデザインUI
- ⚙️ 設定の自動保存（フォルダ、モニター、ソート設定）

## 📦 ダウンロード

以下の2つのバージョンから選択してダウンロードしてください：

### 標準版（推奨）
**PhotoPatto-v1.0.0-win-x64.zip** (67.72 MB)
- ✅ .NET Runtime不要（自己完結型）
- ✅ ダウンロードして即実行可能（単一ファイル）
- 📝 **推奨**: 一般ユーザー、.NET Runtimeがインストールされていない環境

### 軽量版
**PhotoPatto-v1.0.0-win-x64-lightweight.zip** (0.11 MB / 110 KB)
- ✅ **超軽量**（標準版の1/615のサイズ！）
- ⚠️ .NET 10 Runtimeが必要（[ダウンロード](https://dotnet.microsoft.com/download/dotnet/10.0)）
- 📁 複数ファイル構成（exe + dll + 設定ファイル）
- 📝 **推奨**: .NET開発者、既に.NET 10がインストールされている環境

---

**動作環境**: Windows 10/11 (64bit)

## 📖 使い方

1. アプリを起動
2. 「フォルダ選択」ボタンで画像フォルダを選択
3. サムネイル一覧から画像を選択してプレビュー
4. 「出力先」で表示するモニターを選択
5. 「Show」ボタンでフルスクリーン表示
6. キーボードの左右矢印キーで画像を切り替え

詳細は [README.md](https://github.com/iwasakin/PhotoPatto/blob/master/README.md) をご覧ください。

## 🔧 技術仕様

- **フレームワーク**: WPF .NET 10
- **言語**: C# 14.0
- **アーキテクチャ**: Code-behind パターン
- **特徴**: 非同期ストリーミング読み込み、EXIF メタデータ対応

## 📝 変更履歴

初回リリースです。詳細な開発履歴は [CHANGELOG.md](https://github.com/iwasakin/PhotoPatto/blob/master/Docs/CHANGELOG.md) をご覧ください。

---

不具合報告や機能要望は [Issues](https://github.com/iwasakin/PhotoPatto/issues) までお願いします。
