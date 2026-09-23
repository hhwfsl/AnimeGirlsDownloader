using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace AnimeGirlsDownloader;

public sealed partial class AvatarCropDialog : ContentDialog, IDisposable
{
    private const int OutputSize = 512;
    private const int PreviewSize = 280;
    private readonly SKBitmap _source;
    private readonly WriteableBitmap _previewBitmap = new(PreviewSize, PreviewSize);
    private readonly Ellipse _preview;
    private float _zoom = 1f;
    private float _offsetX;
    private float _offsetY;
    private bool _mouseDragging;
    private Point _lastPointerPosition;

    public AvatarCropDialog(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _source = SKBitmap.Decode(filePath)
            ?? throw new InvalidOperationException("The selected file is not a supported image.");

        Title = AppResourceLoader.GetString("AvatarCrop_Title");
        PrimaryButtonText = AppResourceLoader.GetString("AvatarCrop_Save");
        CloseButtonText = AppResourceLoader.GetString("AvatarCrop_Cancel");
        DefaultButton = ContentDialogButton.Primary;

        _preview = new Ellipse
        {
            Width = PreviewSize,
            Height = PreviewSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Fill = new ImageBrush { ImageSource = _previewBitmap, Stretch = Stretch.Fill },
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 2,
            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.Scale,
        };
        _preview.ManipulationDelta += Preview_ManipulationDelta;
        _preview.PointerPressed += Preview_PointerPressed;
        _preview.PointerMoved += Preview_PointerMoved;
        _preview.PointerReleased += Preview_PointerReleased;
        _preview.PointerCanceled += Preview_PointerCanceled;
        _preview.PointerCaptureLost += Preview_PointerCaptureLost;
        _preview.PointerWheelChanged += Preview_PointerWheelChanged;

        var hint = new TextBlock
        {
            Text = AppResourceLoader.GetString("AvatarCrop_GestureHint"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = PreviewSize,
        };
        var content = new StackPanel { Spacing = 10, MinWidth = 320 };
        content.Children.Add(_preview);
        content.Children.Add(hint);
        Content = content;
        RenderPreview();
    }

    public byte[] GetCroppedPng()
    {
        using SKBitmap output = RenderCrop(OutputSize, clipToCircle: true);
        using SKImage image = SKImage.FromBitmap(output);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("The cropped avatar could not be encoded.");
        return data.ToArray();
    }

    public void Dispose()
    {
        _preview.ManipulationDelta -= Preview_ManipulationDelta;
        _preview.PointerPressed -= Preview_PointerPressed;
        _preview.PointerMoved -= Preview_PointerMoved;
        _preview.PointerReleased -= Preview_PointerReleased;
        _preview.PointerCanceled -= Preview_PointerCanceled;
        _preview.PointerCaptureLost -= Preview_PointerCaptureLost;
        _preview.PointerWheelChanged -= Preview_PointerWheelChanged;
        _source.Dispose();
    }

    private SKBitmap RenderCrop(int size, bool clipToCircle)
    {
        var output = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(output);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.Clear(SKColors.Transparent);
        if (clipToCircle)
        {
            using var circle = new SKPath();
            circle.AddCircle(size / 2f, size / 2f, size / 2f);
            canvas.ClipPath(circle, antialias: true);
        }

        SKRectI sourceRect = GetSourceCropRect();
        canvas.DrawBitmap(_source, sourceRect, new SKRect(0, 0, size, size), paint);
        return output;
    }

    private SKRectI GetSourceCropRect()
    {
        int cropSize = Math.Max(1, (int)Math.Round(Math.Min(_source.Width, _source.Height) / _zoom));
        int maxLeft = Math.Max(0, _source.Width - cropSize);
        int maxTop = Math.Max(0, _source.Height - cropSize);
        int left = Math.Clamp((int)Math.Round((1f - _offsetX) * maxLeft / 2f), 0, maxLeft);
        int top = Math.Clamp((int)Math.Round((1f - _offsetY) * maxTop / 2f), 0, maxTop);
        return new SKRectI(left, top, left + cropSize, top + cropSize);
    }

    private void RenderPreview()
    {
        using SKBitmap bitmap = RenderCrop(PreviewSize, clipToCircle: false);
        byte[] pixels = new byte[bitmap.ByteCount];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        using Stream stream = _previewBitmap.PixelBuffer.AsStream();
        stream.Position = 0;
        stream.Write(pixels, 0, pixels.Length);
        _previewBitmap.Invalidate();
    }

    private void Pan(double horizontal, double vertical)
    {
        _offsetX = Math.Clamp(_offsetX + (float)(horizontal * 2d / PreviewSize), -1f, 1f);
        _offsetY = Math.Clamp(_offsetY + (float)(vertical * 2d / PreviewSize), -1f, 1f);
        RenderPreview();
    }

    private void Zoom(float factor)
    {
        _zoom = Math.Clamp(_zoom * factor, 1f, 3f);
        RenderPreview();
    }

    private void Preview_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        Pan(e.Delta.Translation.X, e.Delta.Translation.Y);
        if (Math.Abs(e.Delta.Scale - 1f) > 0.001f)
            Zoom(e.Delta.Scale);
        e.Handled = true;
    }

    private void Preview_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_preview).Properties.IsLeftButtonPressed) return;
        _mouseDragging = _preview.CapturePointer(e.Pointer);
        _lastPointerPosition = e.GetCurrentPoint(_preview).Position;
        e.Handled = true;
    }

    private void Preview_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_mouseDragging) return;
        Point current = e.GetCurrentPoint(_preview).Position;
        Pan(current.X - _lastPointerPosition.X, current.Y - _lastPointerPosition.Y);
        _lastPointerPosition = current;
        e.Handled = true;
    }

    private void Preview_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_mouseDragging) return;
        _preview.ReleasePointerCapture(e.Pointer);
        _mouseDragging = false;
        e.Handled = true;
    }

    private void Preview_PointerCanceled(object sender, PointerRoutedEventArgs e) => _mouseDragging = false;

    private void Preview_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _mouseDragging = false;

    private void Preview_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(_preview).Properties.MouseWheelDelta;
        if (delta != 0)
        {
            Zoom(delta > 0 ? 1.1f : 0.9f);
            e.Handled = true;
        }
    }
}
