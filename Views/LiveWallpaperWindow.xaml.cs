using System;
using System.Windows;

namespace ZeroWall.Views;

public partial class LiveWallpaperWindow : Window
{
    public LiveWallpaperWindow()
    {
        InitializeComponent();
    }

    public void OpenVideo(string videoFilePath, double volume = 0.0)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath)) return;
        Player.Source = new Uri(videoFilePath, UriKind.Absolute);
        Player.Volume = volume;
        Player.Play();
    }

    public void Play()
    {
        Player.Play();
    }

    public void Pause()
    {
        Player.Pause();
    }

    public void SetVolume(double volume)
    {
        Player.Volume = Math.Clamp(volume, 0.0, 1.0);
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        // Infinite loop
        Player.Position = TimeSpan.Zero;
        Player.Play();
    }
}
