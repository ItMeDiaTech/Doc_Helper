# WPF Value Converters Documentation

This directory contains value converters for the Bulk Editor WPF application. Value converters bridge the gap between ViewModels (which store data in logical formats) and XAML UI (which needs specific display formats).

## Available Converters

### 1. BooleanToVisibilityConverter

**Purpose**: Converts boolean values to WPF Visibility enum values.

**Usage**:

```xml
<!-- Show progress bar only when processing -->
<ProgressBar Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}}" />

<!-- Hide button when processing (inverse) -->
<Button Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}, ConverterParameter=inverse}" />
```

### 2. InverseBooleanConverter

**Purpose**: Inverts boolean values (True becomes False, False becomes True).

**Usage**:

```xml
<!-- Disable button when processing -->
<Button IsEnabled="{Binding IsProcessing, Converter={StaticResource InverseBoolConverter}}" />
```

### 3. StatusToColorConverter

**Purpose**: Converts status strings to appropriate colors for visual feedback.

**Supported Values**: Processing (Orange), Complete/Success (Green), Error/Failed (Red), Cancelled (Gray), Ready (Blue), Warning (Goldenrod)

**Usage**:

```xml
<!-- Status text color changes based on processing state -->
<TextBlock Text="{Binding StatusMessage}"
           Foreground="{Binding StatusMessage, Converter={StaticResource StatusToColorConverter}}" />
```

### 4. FileCountToStringConverter

**Purpose**: Converts file count numbers to descriptive text.

**Usage**:

```xml
<!-- Shows "3 files selected" instead of just "3" -->
<TextBlock Text="{Binding SelectedFiles, Converter={StaticResource FileCountToStringConverter}}" />
```

### 5. ProgressToPercentageConverter

**Purpose**: Converts numeric progress values to percentage strings.

**Usage**:

```xml
<!-- Shows "75.3%" instead of raw number -->
<TextBlock Text="{Binding ProgressValue, Converter={StaticResource ProgressToPercentageConverter}}" />
```

### 6. DateTimeToStringConverter

**Purpose**: Converts DateTime values to formatted strings with various format options.

**Format Parameters**:

- "short" → "HH:mm:ss"
- "date" → "yyyy-MM-dd"
- "time" → "HH:mm:ss"
- "datetime" → "yyyy-MM-dd HH:mm:ss"
- "friendly" → "MMM dd, yyyy HH:mm"
- "relative" → "5 minutes ago", "Just now", etc.
- Custom format strings

**Usage**:

```xml
<!-- Relative time display -->
<TextBlock Text="{Binding Timestamp, Converter={StaticResource DateTimeToStringConverter}, ConverterParameter=relative}" />

<!-- Custom format -->
<TextBlock Text="{Binding CreatedDate, Converter={StaticResource DateTimeToStringConverter}, ConverterParameter='MMM dd, yyyy'}" />
```

### 7. FileExtensionToIconConverter

**Purpose**: Converts file extensions to appropriate emoji icons.

**Supported Extensions**: .docx (📄), .xlsx (📊), .pdf (📕), .txt (📝), .zip (📦), .exe (⚙️), .html (🌐), .jpg (🖼️), .mp4 (🎬), .mp3 (🎵), and more.

**Usage**:

```xml
<!-- Shows file icon based on extension -->
<TextBlock Text="{Binding FilePath, Converter={StaticResource FileExtensionToIconConverter}}" />
```

### 8. CountToVisibilityConverter

**Purpose**: Converts count values to Visibility (Count > 0 = Visible, Count = 0 = Collapsed).

**Usage**:

```xml
<!-- Show panel only when there are items -->
<StackPanel Visibility="{Binding Items, Converter={StaticResource CountToVisConverter}}" />

<!-- Hide panel when there are items (inverse) -->
<TextBlock Text="No items found"
           Visibility="{Binding Items, Converter={StaticResource CountToVisConverter}, ConverterParameter=inverse}" />
```

### 9. ProcessingResultToColorConverter

**Purpose**: Converts processing success/failure results to appropriate colors.

**Usage**:

```xml
<!-- Result indicator color -->
<Ellipse Fill="{Binding ProcessingResult, Converter={StaticResource ProcessingResultToColorConverter}}" />
```

## Registration in App.xaml

All converters are registered globally in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <!-- Value Converters -->
        <converters:BooleanToVisibilityConverter x:Key="BoolToVisConverter" />
        <converters:InverseBooleanConverter x:Key="InverseBoolConverter" />
        <converters:StatusToColorConverter x:Key="StatusToColorConverter" />
        <!-- ... and more -->
    </ResourceDictionary>
</Application.Resources>
```

## Real-World Examples

### File Processing UI

```xml
<!-- File list with icons and count -->
<StackPanel>
    <TextBlock Text="{Binding SelectedFiles, Converter={StaticResource FileCountToStringConverter}}" />
    <ListBox ItemsSource="{Binding SelectedFiles}">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding Converter={StaticResource FileExtensionToIconConverter}}" />
                    <TextBlock Text="{Binding}" Margin="5,0,0,0" />
                </StackPanel>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</StackPanel>
```

### Progress Display

```xml
<!-- Progress with percentage and status color -->
<StackPanel>
    <TextBlock Text="{Binding ProgressValue, Converter={StaticResource ProgressToPercentageConverter}}" />
    <TextBlock Text="{Binding StatusMessage}"
               Foreground="{Binding StatusMessage, Converter={StaticResource StatusToColorConverter}}" />
    <ProgressBar Value="{Binding ProgressValue}"
                 Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}}" />
</StackPanel>
```

### Dynamic UI States

```xml
<!-- UI that changes based on processing state -->
<Grid>
    <!-- Processing controls -->
    <StackPanel Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}}">
        <Button Content="Cancel" Command="{Binding CancelCommand}" />
        <TextBlock Text="Processing..." />
    </StackPanel>

    <!-- Ready controls -->
    <StackPanel Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}, ConverterParameter=inverse}">
        <Button Content="Start Processing" Command="{Binding StartCommand}" />
        <TextBlock Text="Ready to process" />
    </StackPanel>
</Grid>
```

These converters provide a complete foundation for building responsive, data-driven WPF user interfaces following MVVM best practices.
