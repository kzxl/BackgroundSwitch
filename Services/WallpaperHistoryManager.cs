namespace ZeroWall.Services;

public class WallpaperHistoryManager
{
    private const int MaxHistoryCount = 30;
    private readonly List<string> _history = [];
    private int _currentIndex = -1;
    private readonly object _lock = new();

    public bool CanGoBack
    {
        get
        {
            lock (_lock)
            {
                return _currentIndex > 0 && _history.Count > 1;
            }
        }
    }

    public bool CanGoForward
    {
        get
        {
            lock (_lock)
            {
                return _currentIndex >= 0 && _currentIndex < _history.Count - 1;
            }
        }
    }

    public string? Current
    {
        get
        {
            lock (_lock)
            {
                if (_currentIndex >= 0 && _currentIndex < _history.Count)
                {
                    return _history[_currentIndex];
                }
                return null;
            }
        }
    }

    public void Push(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;

        lock (_lock)
        {
            // If we are in the middle of history and a new wallpaper is loaded, drop forward history
            if (_currentIndex >= 0 && _currentIndex < _history.Count - 1)
            {
                _history.RemoveRange(_currentIndex + 1, _history.Count - (_currentIndex + 1));
            }

            // Avoid duplicate consecutive entries
            if (_history.Count > 0 && string.Equals(_history[^1], imagePath, StringComparison.OrdinalIgnoreCase))
            {
                _currentIndex = _history.Count - 1;
                return;
            }

            _history.Add(imagePath);

            // Trim oldest if exceeding max limit
            if (_history.Count > MaxHistoryCount)
            {
                _history.RemoveAt(0);
            }

            _currentIndex = _history.Count - 1;
        }
    }

    public string? GetPrevious()
    {
        lock (_lock)
        {
            if (CanGoBack)
            {
                _currentIndex--;
                return _history[_currentIndex];
            }
            return null;
        }
    }

    public string? GetForward()
    {
        lock (_lock)
        {
            if (CanGoForward)
            {
                _currentIndex++;
                return _history[_currentIndex];
            }
            return null;
        }
    }

    public void Remove(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;

        lock (_lock)
        {
            int index = _history.FindIndex(p => string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _history.RemoveAt(index);
                if (_currentIndex >= _history.Count)
                {
                    _currentIndex = _history.Count - 1;
                }
            }
        }
    }
}
