# PhotoPatto 変更履歴

## 概要
このドキュメントは、PhotoPattoアプリケーションの実装過程における主要な変更点と機能追加をまとめたものです。

---

## 実装フェーズ

### Phase 1: プロジェクト基盤構築

#### プロジェクト構成
- **.NET 10** WPFプロジェクトとして作成
- `UseWindowsForms` を有効化（フォルダ選択とマルチモニタサポート用）
- フォルダ構成:
  - `Models/` - データモデル
  - `Services/` - ビジネスロジックとサービス層
  - `Docs/` - ドキュメント

#### 実装ファイル
- `App.xaml` / `App.xaml.cs` - アプリケーションエントリポイント
- `MainWindow.xaml` / `MainWindow.xaml.cs` - メインウィンドウ
- `FullscreenWindow.xaml` / `FullscreenWindow.xaml.cs` - フルスクリーン表示ウィンドウ
- `Models/ImageItem.cs` - 画像データモデル
- `Services/ImageLoader.cs` - 画像読み込みサービス
- `Services/SettingsManager.cs` - 設定永続化サービス

---

### Phase 2: コア機能実装

#### 2.1 データモデル (`ImageItem.cs`)
- `FilePath` - 画像ファイルパス
- `DateModified` - 更新日時
- `Thumbnail` - サムネイル画像
- `Rotation` - 回転角度（0/90/180/270度）
- `INotifyPropertyChanged` 実装による双方向バインディング

#### 2.2 画像読み込みサービス (`ImageLoader.cs`)
**主要機能:**
- `LoadFromFolderStreamAsync()` - ストリーミング型サムネイル読み込み
  - `IAsyncEnumerable<ImageItem>` を使用して1枚ずつ yield
  - 大量画像でも初期表示が高速
- `CreateThumbnail()` - メモリ効率的なサムネイル生成
  - `DecodePixelWidth = 200` で縮小読み込み
  - **EXIF Orientation対応**: メタデータ（タグ274）を読み取り自動回転
  - 対応回転: 90°、180°、270°
- `LoadPreviewAsync()` - 高解像度プレビュー読み込み
  - `DecodePixelWidth = 1600` で高品質表示
  - EXIF自動回転も適用

**対応フォーマット:** `.jpg`, `.jpeg`, `.png`, `.bmp`

#### 2.3 設定管理サービス (`SettingsManager.cs`)
**永続化項目:**
- `LastFolder` - 最後に開いたフォルダ
- `MonitorIndex` - 選択中のモニター番号
- `FadeMilliseconds` - クロスフェード時間（デフォルト500ms）
- `SortKey` - ソートキー（"FileName" / "DateModified"）
- `SortDesc` - 降順ソートフラグ

**実装詳細:**
- JSON形式で `settings.json` に保存
- `Load()` - 起動時に自動読み込み
- `Save()` - ウィンドウクローズ時に自動保存

---

### Phase 3: メインUI実装

#### 3.1 レイアウト構成 (`MainWindow.xaml`)
**2ペインレイアウト:**
```
+----------------------------------------------------------+
| [左ペイン: 2*]          | [右ペイン: 1*]                 |
| - ツールバー             | - ツールバー（2行）             |
| - サムネイルグリッド      | - プレビュー画像                |
|                         | - ファイル名                    |
+----------------------------------------------------------+
| [ステータスバー]                                          |
+----------------------------------------------------------+
```

**左ペイン（サムネイル）:**
- フォルダ選択ボタン
- Prev/Nextボタン
- ソートコンボボックス（ファイル名/更新日付）
- ソート順トグル（昇順/降順）
- フォルダパス表示
- サムネイル一覧（WrapPanel グリッド表示）

**右ペイン（プレビュー）:**
- 出力先モニター選択
- 回転ボタン（左回転/右回転）
- Blackトグル（黒画面表示）
- Showトグル（フルスクリーン表示切替）
- 16:9アスペクト比プレビュー
- 選択ファイル名表示

#### 3.2 サムネイル表示
**実装方式:**
- `ListBox` + `WrapPanel` でグリッドレイアウト
- `VirtualizingStackPanel` 使用でパフォーマンス最適化
- カスタム `DataTemplate`:
  - 150x100ピクセル画像
  - `LayoutTransform` で回転表示
  - ファイル名（3行まで、省略表示）
  - 白背景、角丸ボーダー

**ストリーミング読み込み:**
```csharp
await foreach (var img in ImageLoader.LoadFromFolderStreamAsync(folderPath))
{
    _images.Add(img);  // ObservableCollectionに逐次追加
}
```
→ サムネイルが1枚ずつ表示され、待機時間を削減

#### 3.3 プレビュー表示
- `Viewbox` で16:9アスペクト維持
- 1600x900 固定サイズで高品質表示
- 非同期読み込み:
  ```csharp
  var preview = await ImageLoader.LoadPreviewAsync(imagePath);
  PreviewImage.Source = preview;
  ```
- 回転は `LayoutTransform` で適用

---

### Phase 4: フルスクリーン機能

#### 4.1 FullscreenWindow実装
**ウィンドウ設定:**
- `WindowStyle="None"` - ボーダーレス
- `Topmost="True"` - 最前面表示
- `Background="Black"` - 黒背景

**クロスフェードアニメーション:**
- 2つの `Image` 要素（ImgA / ImgB）を重ねて配置
- `CrossfadeToImageAsync()` でスムーズ切替:
  1. 非表示側の画像に新しい画像をロード
  2. `DoubleAnimation` で不透明度を0→1にアニメート（500ms）
  3. 表示/非表示を入れ替え

**黒画面機能:**
- `BlackOverlay` Rectangle を最前面に配置
- `IsBlack` プロパティで表示/非表示切替

#### 4.2 マルチモニタサポート
**実装方法:**
- `System.Windows.Forms.Screen.AllScreens` でモニター列挙
- メインウィンドウが表示されているモニターをグレーアウト
- `ShowOnMonitor(int index)` でウィンドウを移動:
  ```csharp
  var screen = Screen.AllScreens[index];
  Left = screen.Bounds.Left;
  Top = screen.Bounds.Top;
  Width = screen.Bounds.Width;
  Height = screen.Bounds.Height;
  WindowState = WindowState.Maximized;
  ```

---

### Phase 5: インタラクション機能

#### 5.1 フォルダ選択
```csharp
var dialog = new System.Windows.Forms.FolderBrowserDialog();
if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
{
    await LoadFolderAsync(dialog.SelectedPath);
}
```
- 選択パスを設定に保存
- 起動時に前回フォルダを自動読み込み

#### 5.2 ソート機能
**ソート方式:**
- ファイル名（昇順/降順）
- 更新日付（昇順/降順）
- `ObservableCollection` を直接並べ替え:
  ```csharp
  var sorted = sortDesc 
      ? _images.OrderByDescending(keySelector)
      : _images.OrderBy(keySelector);
  
  _images.Clear();
  foreach (var item in sorted) _images.Add(item);
  ```

#### 5.3 ナビゲーション
**Prev/Next ボタン:**
- 選択インデックスを ±1
- 両端でループしない（無効化）
- `ScrollIntoView()` で画面外サムネイルを自動スクロール

**キーボード操作:**
- 左矢印: 前の画像
- 右矢印: 次の画像
- メインウィンドウとフルスクリーンウィンドウ両方で動作
- コールバック方式でフルスクリーン→メイン通信:
  ```csharp
  fullscreenWindow.OnNavigationKeyPressed = (isLeft) =>
  {
      if (isLeft) NavigatePrev(); else NavigateNext();
  };
  ```

#### 5.4 回転機能
**実装詳細:**
- `ImageItem.Rotation` プロパティを 90度単位で変更
- 全ビューに即座に反映:
  - サムネイル: `LayoutTransform` バインディング
  - プレビュー: `RotateTransform` 直接適用
  - フルスクリーン: `RotateTransform` 直接適用
- 左回転: `Rotation -= 90` (mod 360)
- 右回転: `Rotation += 90` (mod 360)

**注意:** 回転状態は設定ファイルに保存されない（セッションのみ有効）

#### 5.5 Show/Hideトグル
**動作仕様:**
- **Show ON**: フルスクリーンウィンドウを表示し、選択画像を出力
- **Show OFF**: フルスクリーンウィンドウを非表示（シングルモニター環境で便利）
- 画像選択時:
  - Show ONの場合のみフルスクリーンを自動更新
  - Show OFFの場合はプレビューのみ更新

---

### Phase 6: UIデザイン刷新（モダンフラットデザイン）

#### 6.1 カラースキーム
**ベースカラー:**
- 背景: `#F5F5F5` (ライトグレー)
- パネル背景: `#FFFFFF` (ホワイト)
- テキスト: `#333333` (ダークグレー)
- ボーダー: `#E0E0E0` (ソフトグレー)

**アクセントカラー:**
- プライマリ: `#2196F3` (Material Blue)
- ホバー: `#1976D2` (Dark Blue)
- アクティブ: `#4CAF50` (Material Green)
- ホバーアクティブ: `#388E3C` (Dark Green)

#### 6.2 ボタンスタイル
**ModernButton (通常ボタン):**
```xaml
<Style x:Key="ModernButton" TargetType="Button">
    <Setter Property="Background" Value="#FFFFFF"/>
    <Setter Property="BorderBrush" Value="#DDDDDD"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Cursor" Value="Hand"/>
    <!-- IsMouseOver: Blue hover (#E3F2FD background, #2196F3 border) -->
    <!-- IsPressed: Lighter blue (#BBDEFB) -->
</Style>
```

**AccentButton (プライマリアクション):**
```xaml
<Style x:Key="AccentButton" BasedOn="{StaticResource ModernButton}">
    <Setter Property="Background" Value="#2196F3"/>
    <Setter Property="Foreground" Value="White"/>
    <!-- IsMouseOver: Dark blue (#1976D2) -->
</Style>
```

**ModernToggleButton (トグルボタン):**
```xaml
<Style x:Key="ModernToggleButton" TargetType="ToggleButton">
    <!-- Unchecked: White background -->
    <!-- Checked: Green background (#4CAF50) + White text -->
    <!-- Hover when checked: Dark green (#388E3C) -->
</Style>
```

**ModernComboBox:**
```xaml
<Style x:Key="ModernComboBox" TargetType="ComboBox">
    <Setter Property="Background" Value="#FFFFFF"/>
    <Setter Property="BorderBrush" Value="#DDDDDD"/>
    <Setter Property="FontSize" Value="13"/>
</Style>
```

#### 6.3 レイアウト改善
**ボーダー:**
- `CornerRadius="6"` で角丸表現
- `Padding="12"` で余白増加
- `BorderThickness="1"` で控えめな線

**グリッドスプリッター:**
- `Background="#E0E0E0"` でボーダーと統一

**サムネイルカード:**
- 白背景 + 角丸ボーダー
- ホバー時の視覚フィードバック（ListBoxの選択状態）

#### 6.4 サムネイル選択時の視覚的改善
**選択状態の明確化（重要な改善）:**
- **選択時**: 太い青枠線（3px、#2196F3）+ 薄青背景（#E3F2FD）
- **ホバー時**: 薄青ボーダー（#BBDEFB）
- **通常時**: ソフトグレー枠線（1px、#E0E0E0）+ 白背景

**実装詳細:**
```xaml
<ListBox.ItemContainerStyle>
    <Style TargetType="ListBoxItem">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="ItemBorder" BorderBrush="#E0E0E0" BorderThickness="1" 
                            Margin="8" Padding="4" Width="184" CornerRadius="4" Background="White">
                        <ContentPresenter />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="ItemBorder" Property="BorderBrush" Value="#2196F3"/>
                            <Setter TargetName="ItemBorder" Property="BorderThickness" Value="3"/>
                            <Setter TargetName="ItemBorder" Property="Background" Value="#E3F2FD"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ListBox.ItemContainerStyle>
```

**サイズ調整:**
- `ItemWidth`: 200px（太い枠線を含む十分なスペース確保）
- `Border Width`: 184px（画像150px + パディング・枠線考慮）
- `Margin`: 8px（枠線が隣接アイテムと干渉しないよう余裕を持たせた）

#### 6.5 プレビューペインのレイアウト調整
**コントロール配置の最適化:**
- **1段目**: 出力先モニター選択
- **2段目**: 回転ボタン（左/右）、Black、Show
- **3段目**: Prev、Next（画像に近い位置で操作しやすく）

**位置調整:**
- すべてのコントロールを左詰め（`HorizontalAlignment="Left"`）
- Prev/Nextボタンをサムネイルペインから移動し、プレビュー操作を集約

#### 6.6 出力先モニター変更の即時反映
**UX改善:**
- モニター選択コンボボックス変更時、即座にフルスクリーンウィンドウを新しいモニターに移動
- `ComboMonitor.SelectionChanged` イベントで `_fsWindow.ShowOnMonitor()` を呼び出し

**実装:**
```csharp
private void ComboMonitor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    // If Show is ON and fullscreen window exists, move it to the new monitor immediately
    if (BtnShow.IsChecked == true && _fsWindow != null)
    {
        try
        {
            var screens = WinForms.Screen.AllScreens;
            int idx = ComboMonitor.SelectedIndex >= 0 ? ComboMonitor.SelectedIndex : 0;
            if (idx >= 0 && idx < screens.Length)
            {
                _fsWindow.ShowOnMonitor(idx);
            }
        }
        catch { }
    }
}
```
→ ユーザーがモニターを切り替えると、リアルタイムでウィンドウが移動


**ステータスバー:**
- 白背景 + 上部ボーダー
- 左右にテキストとQuitボタンを配置

#### 6.7 ホバー/プレスエフェクト
**ボタン:**
- デフォルト: 白背景
- ホバー: 薄青背景（#E3F2FD）+ 青ボーダー（#2196F3）
- プレス: より濃い青背景（#BBDEFB）

**トグルボタン:**
- OFF時ホバー: 青ボーダー
- ON時: 緑背景（#4CAF50）
- ON時ホバー: ダークグリーン背景（#388E3C）

**サムネイル:**
- 通常: ソフトグレー枠線（#E0E0E0）
- ホバー: 薄青ボーダー（#BBDEFB）
- 選択時: 太い青枠（3px、#2196F3）+ 薄青背景（#E3F2FD）

---

## Phase 7: UI/UX 最終調整

### 7.1 視認性とユーザビリティの向上
**実施日: 最終調整フェーズ**

#### サムネイル選択の視覚的強化
**課題:** 選択されたサムネイルがわかりにくい

**解決策:**
1. `ItemContainerStyle` でカスタムテンプレート実装
2. 選択時に太い青枠（3px）+ 背景色変更（#E3F2FD）
3. サムネイルサイズを調整して枠線が切れないよう修正

#### レイアウトの最適化
**Prev/Nextボタンの移動:**
- サムネイルペインから削除 → プレビューペインの3段目に移動
- 理由: プレビュー画像の近くに配置することで操作性向上

**コントロール配置:**
- すべてのプレビューコントロールを左詰めに統一
- 視線移動を最小化

#### 即時フィードバック機能
**出力先モニター変更の即時反映:**
- 従来: モニター選択しても次回Show時まで反映されない
- 改善: 選択と同時にフルスクリーンウィンドウが移動
- 実装: `ComboMonitor.SelectionChanged` イベント追加

### 7.2 最終的なUIスペック

**サムネイルグリッド:**
- ItemWidth: 200px
- Border Width: 184px
- Image Size: 150x100px
- Margin: 8px（枠線干渉防止）
- 選択時BorderThickness: 3px

**プレビューペイン（3段構成）:**
1. 出力先モニター選択
2. 回転・表示制御（回転ボタン、Black、Show）
3. ナビゲーション（Prev、Next）

**カラースキーム（確定版）:**
- ウィンドウ背景: #F5F5F5
- パネル背景: #FFFFFF
- アクセント: #2196F3（Blue）
- アクティブ: #4CAF50（Green）
- ボーダー: #E0E0E0
- テキスト: #333333


---

## 技術的な特徴

### 非同期処理
- `async/await` パターンを全面採用
- `IAsyncEnumerable` でストリーミング処理
- `Task.Run()` でバックグラウンド読み込み

### メモリ最適化
- `BitmapImage.DecodePixelWidth` で縮小読み込み
- サムネイル: 200px
- プレビュー: 1600px
- フルスクリーン: フルサイズ

### EXIFメタデータ処理
```csharp
var metadata = bitmap.Metadata as BitmapMetadata;
if (metadata?.GetQuery("/app1/ifd/{ushort=274}") is ushort orientation)
{
    // orientation値に応じて90/180/270度回転
}
```

### データバインディング
- `ObservableCollection<ImageItem>` で自動UI更新
- `INotifyPropertyChanged` で個別プロパティ変更通知
- XAML側で `{Binding ...}` による宣言的バインディング

### 名前空間の曖昧性解決
```csharp
// Forms.Applicationとの衝突回避
System.Windows.Application.Current.Shutdown();

// Forms.Imageとの衝突回避
System.Windows.Controls.Image PreviewImage;

// Forms.KeyEventArgsとの衝突回避
System.Windows.Input.KeyEventArgs e;
```

---

## 既知の制限事項と注意点

### サポート外機能
- 動画ファイルは非対応
- RAW形式（.cr2, .nefなど）は非対応
- 回転状態の永続化なし（セッション内のみ）
- サムネイルキャッシュなし（再起動時に再生成）

### WPF/.NET 10 制約
- `StackPanel.Spacing` 非対応 → `Margin` で代替
- `TextBlock.MaxLines` 非対応 → `MaxHeight` で代替
- `UseWindowsForms` による名前空間衝突に注意

### パフォーマンス考慮事項
- 10000枚以上の画像フォルダではメモリ使用量増加
- フルスクリーンクロスフェード中は重い処理を避ける
- VirtualizingStackPanel有効化済み

---

## ファイル構成

```
PhotoPresenter/
├── PhotoPresenter.csproj          # プロジェクトファイル (.NET 10, UseWindowsForms)
├── App.xaml                       # アプリケーションリソース定義
├── App.xaml.cs                    # アプリケーションエントリポイント
├── MainWindow.xaml                # メインウィンドウUI (モダンデザイン適用済み)
├── MainWindow.xaml.cs             # メインウィンドウロジック
├── FullscreenWindow.xaml          # フルスクリーンウィンドウUI
├── FullscreenWindow.xaml.cs       # フルスクリーンウィンドウロジック
├── Models/
│   └── ImageItem.cs               # 画像データモデル (INotifyPropertyChanged)
├── Services/
│   ├── ImageLoader.cs             # 画像読み込みサービス (EXIF対応)
│   └── SettingsManager.cs         # 設定永続化サービス (JSON)
└── settings.json                  # 設定ファイル (自動生成)

Docs/
├── PhotoPresenter.md              # 機能仕様書
└── CHANGELOG.md                   # 本ドキュメント
```

---

## 今後の拡張候補

### 機能追加案
- [ ] **動画対応**（MP4, MOV等）- MediaElement使用、サムネイル自動生成
- [ ] スライドショーモード（自動再生）
- [ ] お気に入りマーク機能
- [ ] 画像のコピー/移動機能
- [ ] フォルダ履歴
- [ ] Escキーでフルスクリーン終了
- [ ] ズーム機能（プレビュー/フルスクリーン）
- [ ] 回転状態の永続化

### 技術改善案
- [ ] サムネイルキャッシュ（高速再読み込み）
- [ ] RAW画像対応（サードパーティライブラリ使用）
- [ ] GPU加速（WriteableBitmap利用）
- [ ] MVVM パターン移行（大規模化時）
- [ ] 単体テスト追加
- [ ] 動画サムネイル自動生成（FFmpeg等）

### 検討中の機能
**動画対応の実現可能性:**
- ✅ WPF標準の`MediaElement`で基本再生可能
- ✅ MediaPlayerでフレーム抽出してサムネイル生成可能
- ⚠️ コーデック依存、メモリ消費増大に注意
- 📝 段階的実装を推奨：Phase 1（再生のみ）→ Phase 2（サムネイル）→ Phase 3（高度な制御）

- [ ] MVVM パターン移行
- [ ] 単体テスト追加

---

## バージョン情報

**現在のバージョン:** 1.0.0  
**対象フレームワーク:** .NET 10  
**開発環境:** Visual Studio 2025 / WPF  
**最終更新日:** 2025年

**変更履歴:**
- **v1.0.0** (2025) - 初回リリース
  - コア機能実装完了
  - モダンフラットデザイン適用
  - UI/UX最終調整（サムネイル選択視認性向上、レイアウト最適化）

---

## まとめ

PhotoPresenterは、.NET 10 WPFをベースに、以下の特徴を持つ画像プレゼンテーションアプリケーションです:

✅ **高速なサムネイル表示** (ストリーミング読み込み、VirtualizingStackPanel)  
✅ **EXIFメタデータ対応** (自動回転、Orientation Tag 274)  
✅ **マルチモニタ対応** (セカンダリディスプレイへの出力、即時切替)  
✅ **スムーズなクロスフェード** (500msアニメーション、DoubleAnimation)  
✅ **モダンなフラットデザイン** (Material Design風カラー、ホバーエフェクト)  
✅ **直感的な操作** (キーボード/マウス両対応、視覚的フィードバック)  
✅ **設定の永続化** (前回の状態を記憶、JSON形式)  
✅ **優れた視認性** (選択サムネイルの明確な表示、太い青枠+背景色)  
✅ **最適化されたレイアウト** (操作性を考慮したコントロール配置)  

すべての機能が統合され、プロフェッショナルなプレゼンテーション環境を提供します。

---

## 開発の振り返り

### 成功したポイント
1. **ストリーミング読み込み**: `IAsyncEnumerable`の採用で初期表示が劇的に高速化
2. **EXIF対応**: メタデータを活用した自動回転で手動調整の手間を削減
3. **段階的なUI改善**: ユーザーフィードバックを元に視認性とレイアウトを継続的に改善
4. **即時反映機能**: モニター変更時の即座な移動でUXが大幅向上

### 技術的な学び
- WPF .NET 10の制約（MaxLines非対応、Spacing非対応）への対処
- UseWindowsFormsによる名前空間衝突の解決
- ItemContainerStyleによる高度なカスタマイズ
- サイズ計算の重要性（Border、Padding、Marginの正確な管理）

### 今後の展望
静止画像プレゼンターとして完成度の高いアプリケーションとなりました。将来的な動画対応により、さらに用途が広がる可能性があります。

