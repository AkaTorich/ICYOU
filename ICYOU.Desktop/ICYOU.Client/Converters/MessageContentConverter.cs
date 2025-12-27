using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICYOU.Client.Services;
using WpfAnimatedGif;

namespace ICYOU.Client.Converters;

public class MessageContentConverter : IValueConverter
{
    private static readonly List<WeakReference<MediaElement>> _activeVideos = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrEmpty(text))
            return new TextBlock();
        
        var textBrush = Application.Current.Resources["TextBrush"] as Brush ?? 
                        new SolidColorBrush(Color.FromRgb(224, 224, 224));
        
        // Проверяем на множественные цитаты
        if (text.StartsWith("[QUOTES|"))
        {
            return CreateMultiQuoteContent(text, textBrush);
        }
        
        // Проверяем на одиночную цитату (обратная совместимость)
        if (text.StartsWith("[QUOTE|"))
        {
            return CreateQuoteContent(text, textBrush);
        }
        
        // Проверяем на медиа-файл (новый формат с | или старый с :)
        if (text.StartsWith("[FILE|") || text.StartsWith("[FILE:"))
        {
            return CreateMediaContent(text, textBrush);
        }
        
        // Проверяем на превью ссылки от модуля
        if (text.Contains("[LINKPREVIEW|"))
        {
            return CreateLinkPreviewFromModule(text, textBrush);
        }
        
        var emoteService = EmoteService.Instance;
        var linkService = LinkPreviewService.Instance;
        var emotes = emoteService.FindEmotesInText(text);
        var hasUrls = linkService.HasUrls(text);
        
        if (emotes.Count == 0 && !hasUrls)
        {
            // Нет смайлов и ссылок - просто текст
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush
            };
        }
        
        if (emotes.Count == 0 && hasUrls)
        {
            // Только ссылки без смайлов
            return CreateTextWithLinks(text, textBrush, linkService);
        }
        
        // Есть смайлы - создаём WrapPanel с текстом и картинками
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        
        var lastIndex = 0;
        foreach (var (start, length, code) in emotes)
        {
            // Добавляем текст до смайла
            if (start > lastIndex)
            {
                var beforeText = text.Substring(lastIndex, start - lastIndex);
                panel.Children.Add(new TextBlock
                {
                    Text = beforeText,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = textBrush
                });
            }
            
            // Добавляем смайл
            var emotePath = emoteService.GetEmotePath(code);
            if (emotePath != null)
            {
                var img = new Image
                {
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0)
                };
                
                // Для GIF используем WpfAnimatedGif
                if (emotePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(emotePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    ImageBehavior.SetAnimatedSource(img, bitmap);
                    ImageBehavior.SetRepeatBehavior(img, System.Windows.Media.Animation.RepeatBehavior.Forever);
                }
                else
                {
                    img.Source = emoteService.GetEmoteImage(code);
                }
                
                panel.Children.Add(img);
            }
            else
            {
                // Смайл не найден - показываем код
                panel.Children.Add(new TextBlock
                {
                    Text = code,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = textBrush
                });
            }
            
            lastIndex = start + length;
        }
        
        // Добавляем текст после последнего смайла
        if (lastIndex < text.Length)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text.Substring(lastIndex),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = textBrush
            });
        }
        
        return panel;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    
    private FrameworkElement CreateMultiQuoteContent(string text, Brush textBrush)
    {
        try
        {
            var endQuotes = text.IndexOf(']');
            if (endQuotes < 0) return CreateTextBlock(text, textBrush);
            
            var quotesData = text.Substring(8, endQuotes - 8); // После "[QUOTES|"
            var replyContent = text.Substring(endQuotes + 1).TrimStart();
            
            // Разбиваем по | - каждый элемент это "sender~content"
            var quoteParts = quotesData.Split('|');
            if (quoteParts.Length < 1) return CreateTextBlock(text, textBrush);
            
            var container = new StackPanel();
            
            foreach (var quotePart in quoteParts)
            {
                var parts = quotePart.Split('~', 2);
                if (parts.Length < 2) continue;

                var sender = parts[0];
                var content = parts[1];

                // Убираем теги [LINKPREVIEW|...] из цитируемого контента
                if (content.Contains("[LINKPREVIEW|"))
                {
                    var previewStart = content.IndexOf("[LINKPREVIEW|");
                    var previewEnd = content.IndexOf("]", previewStart);
                    if (previewEnd > previewStart)
                    {
                        var before = previewStart > 0 ? content.Substring(0, previewStart).Trim() : "";
                        var after = previewEnd + 1 < content.Length ? content.Substring(previewEnd + 1).TrimStart() : "";

                        if (!string.IsNullOrEmpty(before))
                        {
                            content = before;
                        }
                        else if (!string.IsNullOrEmpty(after))
                        {
                            content = after;
                        }
                        else
                        {
                            // Берем title из превью
                            var previewData = content.Substring(previewStart + 13, previewEnd - previewStart - 13);
                            var previewParts = previewData.Split('|');
                            if (previewParts.Length >= 2)
                                content = "🔗 " + previewParts[1].Replace("{{PIPE}}", "|");
                        }
                    }
                }

                // Блок цитаты
                var quoteBox = new Border
                {
                    Background = Application.Current.Resources["CardBrush"] as Brush,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                
                var quoteGrid = new Grid();
                quoteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                quoteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var line = new Border
                {
                    Background = Application.Current.Resources["AccentBrush"] as Brush,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(line, 0);
                quoteGrid.Children.Add(line);
                
                var quoteStack = new StackPanel();
                quoteStack.Children.Add(new TextBlock
                {
                    Text = sender,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Application.Current.Resources["AccentBrush"] as Brush
                });
                quoteStack.Children.Add(new TextBlock
                {
                    Text = content,
                    FontSize = 11,
                    Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 300
                });
                Grid.SetColumn(quoteStack, 1);
                quoteGrid.Children.Add(quoteStack);
                
                quoteBox.Child = quoteGrid;
                container.Children.Add(quoteBox);
            }

            // Текст ответа
            if (!string.IsNullOrWhiteSpace(replyContent))
            {
                // Убираем [LINKPREVIEW|...] из replyContent или показываем только текст
                if (replyContent.Contains("[LINKPREVIEW|"))
                {
                    var previewStart = replyContent.IndexOf("[LINKPREVIEW|");
                    var previewEnd = replyContent.IndexOf("]", previewStart);

                    if (previewEnd > previewStart)
                    {
                        var textBefore = previewStart > 0 ? replyContent.Substring(0, previewStart).Trim() : "";
                        var textAfter = previewEnd + 1 < replyContent.Length ? replyContent.Substring(previewEnd + 1).TrimStart() : "";

                        // Парсим данные превью
                        var previewData = replyContent.Substring(previewStart + 13, previewEnd - previewStart - 13);
                        var previewParts = previewData.Split('|');

                        if (!string.IsNullOrEmpty(textBefore))
                        {
                            container.Children.Add(CreateReplyTextElement(textBefore, textBrush));
                        }

                        // Показываем только текст превью (title + description) без картинки
                        if (previewParts.Length >= 3)
                        {
                            var title = previewParts[1].Replace("{{PIPE}}", "|");
                            var description = previewParts[2].Replace("{{PIPE}}", "|");

                            var previewTextPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

                            if (!string.IsNullOrEmpty(title))
                            {
                                previewTextPanel.Children.Add(new TextBlock
                                {
                                    Text = "🔗 " + title,
                                    FontSize = 12,
                                    FontWeight = FontWeights.SemiBold,
                                    Foreground = textBrush,
                                    TextWrapping = TextWrapping.Wrap
                                });
                            }

                            if (!string.IsNullOrEmpty(description))
                            {
                                previewTextPanel.Children.Add(new TextBlock
                                {
                                    Text = description,
                                    FontSize = 11,
                                    Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 2, 0, 0)
                                });
                            }

                            container.Children.Add(previewTextPanel);
                        }

                        if (!string.IsNullOrEmpty(textAfter))
                        {
                            container.Children.Add(CreateReplyTextElement(textAfter, textBrush));
                        }
                    }
                    else
                    {
                        container.Children.Add(CreateReplyTextElement(replyContent, textBrush));
                    }
                }
                else
                {
                    container.Children.Add(CreateReplyTextElement(replyContent, textBrush));
                }
            }

            return container;
        }
        catch
        {
            return CreateTextBlock(text, textBrush);
        }
    }

    private FrameworkElement CreateQuoteContent(string text, Brush textBrush)
    {
        try
        {
            var endQuote = text.IndexOf(']');
            if (endQuote < 0) return CreateTextBlock(text, textBrush);
            
            var quoteData = text.Substring(7, endQuote - 7); // После "[QUOTE|"
            var parts = quoteData.Split('|', 3);
            
            if (parts.Length < 3) return CreateTextBlock(text, textBrush);
            
            var quotedSender = parts[1];
            var quotedContent = parts[2];
            var replyContent = text.Substring(endQuote + 1).TrimStart();

            // Убираем теги [LINKPREVIEW|...] из цитируемого контента
            if (quotedContent.Contains("[LINKPREVIEW|"))
            {
                var previewStart = quotedContent.IndexOf("[LINKPREVIEW|");
                var previewEnd = quotedContent.IndexOf("]", previewStart);
                if (previewEnd > previewStart)
                {
                    var before = previewStart > 0 ? quotedContent.Substring(0, previewStart).Trim() : "";
                    var after = previewEnd + 1 < quotedContent.Length ? quotedContent.Substring(previewEnd + 1).TrimStart() : "";

                    if (!string.IsNullOrEmpty(before))
                    {
                        quotedContent = before;
                    }
                    else if (!string.IsNullOrEmpty(after))
                    {
                        quotedContent = after;
                    }
                    else
                    {
                        // Берем title из превью
                        var previewData = quotedContent.Substring(previewStart + 13, previewEnd - previewStart - 13);
                        var previewParts = previewData.Split('|');
                        if (previewParts.Length >= 2)
                            quotedContent = "🔗 " + previewParts[1].Replace("{{PIPE}}", "|");
                    }
                }
            }

            var container = new StackPanel();
            
            // Блок цитаты
            var quoteBox = new Border
            {
                Background = Application.Current.Resources["CardBrush"] as Brush,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };
            
            var quoteGrid = new Grid();
            quoteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            quoteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Вертикальная линия
            var line = new Border
            {
                Background = Application.Current.Resources["AccentBrush"] as Brush,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(line, 0);
            quoteGrid.Children.Add(line);
            
            // Текст цитаты
            var quoteStack = new StackPanel();
            quoteStack.Children.Add(new TextBlock
            {
                Text = quotedSender,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Application.Current.Resources["AccentBrush"] as Brush
            });
            quoteStack.Children.Add(new TextBlock
            {
                Text = quotedContent,
                FontSize = 12,
                Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            });
            Grid.SetColumn(quoteStack, 1);
            quoteGrid.Children.Add(quoteStack);
            
            quoteBox.Child = quoteGrid;
            container.Children.Add(quoteBox);

            // Текст ответа
            if (!string.IsNullOrWhiteSpace(replyContent))
            {
                // Убираем [LINKPREVIEW|...] из replyContent или показываем только текст
                if (replyContent.Contains("[LINKPREVIEW|"))
                {
                    var previewStart = replyContent.IndexOf("[LINKPREVIEW|");
                    var previewEnd = replyContent.IndexOf("]", previewStart);

                    if (previewEnd > previewStart)
                    {
                        var textBefore = previewStart > 0 ? replyContent.Substring(0, previewStart).Trim() : "";
                        var textAfter = previewEnd + 1 < replyContent.Length ? replyContent.Substring(previewEnd + 1).TrimStart() : "";

                        // Парсим данные превью
                        var previewData = replyContent.Substring(previewStart + 13, previewEnd - previewStart - 13);
                        var previewParts = previewData.Split('|');

                        if (!string.IsNullOrEmpty(textBefore))
                        {
                            container.Children.Add(CreateReplyTextElement(textBefore, textBrush));
                        }

                        // Показываем только текст превью (title + description) без картинки
                        if (previewParts.Length >= 3)
                        {
                            var title = previewParts[1].Replace("{{PIPE}}", "|");
                            var description = previewParts[2].Replace("{{PIPE}}", "|");

                            var previewTextPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

                            if (!string.IsNullOrEmpty(title))
                            {
                                previewTextPanel.Children.Add(new TextBlock
                                {
                                    Text = "🔗 " + title,
                                    FontSize = 12,
                                    FontWeight = FontWeights.SemiBold,
                                    Foreground = textBrush,
                                    TextWrapping = TextWrapping.Wrap
                                });
                            }

                            if (!string.IsNullOrEmpty(description))
                            {
                                previewTextPanel.Children.Add(new TextBlock
                                {
                                    Text = description,
                                    FontSize = 11,
                                    Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 2, 0, 0)
                                });
                            }

                            container.Children.Add(previewTextPanel);
                        }

                        if (!string.IsNullOrEmpty(textAfter))
                        {
                            container.Children.Add(CreateReplyTextElement(textAfter, textBrush));
                        }
                    }
                    else
                    {
                        container.Children.Add(CreateReplyTextElement(replyContent, textBrush));
                    }
                }
                else
                {
                    container.Children.Add(CreateReplyTextElement(replyContent, textBrush));
                }
            }

            return container;
        }
        catch
        {
            return CreateTextBlock(text, textBrush);
        }
    }
    
    private FrameworkElement CreateReplyTextElement(string replyContent, Brush textBrush)
    {
        var emoteService = EmoteService.Instance;
        var emotes = emoteService.FindEmotesInText(replyContent);
        
        if (emotes.Count == 0)
        {
            return new TextBlock
            {
                Text = replyContent,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush
            };
        }
        
        // Текст со смайлами
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        var lastIndex = 0;
        
        foreach (var (start, length, code) in emotes)
        {
            if (start > lastIndex)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = replyContent.Substring(lastIndex, start - lastIndex),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = textBrush
                });
            }
            
            var emotePath = emoteService.GetEmotePath(code);
            if (emotePath != null)
            {
                var img = new Image { Width = 24, Height = 24, Stretch = Stretch.Uniform, Margin = new Thickness(2, 0, 2, 0) };
                if (emotePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = new BitmapImage(new Uri(emotePath));
                    ImageBehavior.SetAnimatedSource(img, bitmap);
                }
                else
                {
                    img.Source = emoteService.GetEmoteImage(code);
                }
                panel.Children.Add(img);
            }
            else
            {
                panel.Children.Add(new TextBlock { Text = code, Foreground = textBrush });
            }
            lastIndex = start + length;
        }
        
        if (lastIndex < replyContent.Length)
        {
            panel.Children.Add(new TextBlock
            {
                Text = replyContent.Substring(lastIndex),
                Foreground = textBrush
            });
        }
        
        return panel;
    }
    
    private TextBlock CreateTextBlock(string text, Brush brush)
    {
        return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = brush };
    }
    
    private FrameworkElement CreateTextWithLinks(string text, Brush textBrush, LinkPreviewService linkService)
    {
        var urls = linkService.FindUrlsInText(text);
        if (urls.Count == 0)
            return CreateTextBlock(text, textBrush);
        
        var container = new StackPanel();
        var accentBrush = Application.Current.Resources["AccentBrush"] as Brush ?? Brushes.Green;
        
        // Текст с кликабельными ссылками
        var textPanel = new WrapPanel();
        var lastIndex = 0;
        
        foreach (var (start, length, url) in urls)
        {
            // Текст до ссылки
            if (start > lastIndex)
            {
                textPanel.Children.Add(new TextBlock
                {
                    Text = text.Substring(lastIndex, start - lastIndex),
                    Foreground = textBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            
            // Кликабельная ссылка
            var linkText = new TextBlock
            {
                Text = url,
                Foreground = accentBrush,
                TextDecorations = TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            linkText.MouseLeftButtonUp += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            textPanel.Children.Add(linkText);
            
            lastIndex = start + length;
        }
        
        // Оставшийся текст
        if (lastIndex < text.Length)
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = text.Substring(lastIndex),
                Foreground = textBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        
        container.Children.Add(textPanel);
        
        // Загружаем превью для первой ссылки асинхронно
        var firstUrl = urls[0].Url;
        LoadLinkPreviewAsync(container, firstUrl, textBrush, linkService);
        
        return container;
    }
    
    private async void LoadLinkPreviewAsync(StackPanel container, string url, Brush textBrush, LinkPreviewService linkService)
    {
        try
        {
            var preview = await linkService.GetPreviewAsync(url);
            if (preview != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var previewElement = linkService.CreatePreviewElement(preview, textBrush);
                    container.Children.Add(previewElement);
                });
            }
        }
        catch { }
    }
    
    private FrameworkElement CreateMediaContent(string text, Brush textBrush)
    {
        var fileService = FileTransferService.Instance;
        var (fileType, fileName, data, savedPath) = fileService.ParseFileMessage(text);
        
        if (data == null || string.IsNullOrEmpty(fileType))
            return new TextBlock { Text = "[Ошибка загрузки]", Foreground = textBrush };
        
        return fileType switch
        {
            "image" => CreateImage(data, fileName!, savedPath),
            "video" => CreateVideo(data, fileName!, savedPath),
            "audio" => CreateAudio(data, fileName!, savedPath),
            _ => CreateFileInfo(data, fileName!, textBrush, savedPath)
        };
    }
    
    private FrameworkElement CreateImage(byte[] data, string fileName, string? savedPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = new MemoryStream(data);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        
        var image = new Image
        {
            Source = bitmap,
            MaxWidth = 400,
            MaxHeight = 300,
            Stretch = Stretch.Uniform
        };
        
        if (fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            ImageBehavior.SetAnimatedSource(image, bitmap);
            ImageBehavior.SetRepeatBehavior(image, System.Windows.Media.Animation.RepeatBehavior.Forever);
        }
        
        // Контейнер с картинкой и кнопкой сохранения
        var container = new Grid();
        container.Children.Add(image);
        
        var saveBtn = new Button
        {
            Content = "💾",
            Width = 30,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 5, 0),
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        saveBtn.Click += (s, e) => SaveFileWithPath(fileName, data, savedPath);
        container.Children.Add(saveBtn);
        
        return container;
    }
    
    private FrameworkElement CreateVideo(byte[] data, string fileName, string? savedPath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"icyou_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        File.WriteAllBytes(tempPath, data);
        
        var grid = new Grid { MaxWidth = 400, MaxHeight = 300 };
        
        var media = new MediaElement
        {
            Source = new Uri(tempPath),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            MaxWidth = 400,
            MaxHeight = 300,
            Volume = 0.5
        };
        
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 5),
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0))
        };
        
        var playBtn = CreateControlButton("▶");
        var pauseBtn = CreateControlButton("⏸");
        var saveBtn = CreateControlButton("💾");
        
        playBtn.Click += (s, e) => media.Play();
        pauseBtn.Click += (s, e) => media.Pause();
        saveBtn.Click += (s, e) => SaveFileWithPath(fileName, data, savedPath);
        
        controls.Children.Add(playBtn);
        controls.Children.Add(pauseBtn);
        controls.Children.Add(saveBtn);
        
        grid.Children.Add(media);
        grid.Children.Add(controls);
        
        _activeVideos.Add(new WeakReference<MediaElement>(media));
        
        media.Unloaded += (s, e) =>
        {
            media.Stop();
            try { File.Delete(tempPath); } catch { }
        };
        
        return grid;
    }
    
    private FrameworkElement CreateAudio(byte[] data, string fileName, string? savedPath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"icyou_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        File.WriteAllBytes(tempPath, data);
        
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        
        var media = new MediaElement
        {
            Source = new Uri(tempPath),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            Volume = 0.5,
            Width = 0,
            Height = 0
        };
        
        var isPlaying = false;
        var playBtn = new Button
        {
            Content = "▶ " + fileName,
            Padding = new Thickness(10, 5, 10, 5),
            Background = Application.Current.Resources["PrimaryBrush"] as Brush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        
        playBtn.Click += (s, e) =>
        {
            if (isPlaying) { media.Pause(); playBtn.Content = "▶ " + fileName; }
            else { media.Play(); playBtn.Content = "⏸ " + fileName; }
            isPlaying = !isPlaying;
        };
        
        media.MediaEnded += (s, e) =>
        {
            isPlaying = false;
            playBtn.Content = "▶ " + fileName;
            media.Position = TimeSpan.Zero;
        };
        
        var saveBtn = new Button
        {
            Content = "💾",
            Margin = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(5),
            Background = Application.Current.Resources["CardBrush"] as Brush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        saveBtn.Click += (s, e) => SaveFileWithPath(fileName, data, savedPath);
        
        panel.Children.Add(media);
        panel.Children.Add(playBtn);
        panel.Children.Add(saveBtn);
        
        media.Unloaded += (s, e) =>
        {
            media.Stop();
            try { File.Delete(tempPath); } catch { }
        };
        
        return panel;
    }
    
    private FrameworkElement CreateFileInfo(byte[] data, string fileName, Brush textBrush, string? savedPath)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        
        panel.Children.Add(new TextBlock { Text = "📎", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        
        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = fileName, Foreground = textBrush, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = FormatSize(data.Length), Foreground = Application.Current.Resources["TextSecondaryBrush"] as Brush, FontSize = 11 });
        panel.Children.Add(info);
        
        var saveBtn = new Button
        {
            Content = "Сохранить как...",
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Application.Current.Resources["PrimaryBrush"] as Brush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        saveBtn.Click += (s, e) => SaveFileWithPath(fileName, data, savedPath);
        panel.Children.Add(saveBtn);
        
        return panel;
    }
    
    private FrameworkElement CreateLinkPreviewFromModule(string text, Brush textBrush)
    {
        var mainPanel = new StackPanel();
        
        // Парсим формат: текст...[LINKPREVIEW|url|title|description|imageUrl|siteName]
        var previewStart = text.IndexOf("[LINKPREVIEW|");
        var previewEnd = text.IndexOf("]", previewStart);
        
        // Текст до превью
        var textBefore = previewStart > 0 ? text.Substring(0, previewStart).Trim() : "";
        
        if (!string.IsNullOrEmpty(textBefore))
        {
            // Ссылка в тексте - делаем кликабельной
            var linkPanel = new WrapPanel();
            var urlMatch = System.Text.RegularExpressions.Regex.Match(textBefore, @"(https?://[^\s]+)");
            if (urlMatch.Success)
            {
                var beforeUrl = textBefore.Substring(0, urlMatch.Index);
                var url = urlMatch.Value;
                var afterUrl = textBefore.Substring(urlMatch.Index + url.Length);
                
                if (!string.IsNullOrEmpty(beforeUrl))
                    linkPanel.Children.Add(new TextBlock { Text = beforeUrl, Foreground = textBrush, TextWrapping = TextWrapping.Wrap });
                
                var linkText = new TextBlock 
                { 
                    Text = url, 
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 255)),
                    TextWrapping = TextWrapping.Wrap,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    TextDecorations = TextDecorations.Underline
                };
                linkText.MouseLeftButtonUp += (s, e) => OpenUrl(url);
                linkPanel.Children.Add(linkText);
                
                if (!string.IsNullOrEmpty(afterUrl))
                    linkPanel.Children.Add(new TextBlock { Text = afterUrl, Foreground = textBrush, TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                linkPanel.Children.Add(new TextBlock { Text = textBefore, Foreground = textBrush, TextWrapping = TextWrapping.Wrap });
            }
            mainPanel.Children.Add(linkPanel);
        }
        
        // Парсим данные превью
        if (previewEnd > previewStart)
        {
            var previewData = text.Substring(previewStart + 13, previewEnd - previewStart - 13);
            var parts = previewData.Split('|');
            
            if (parts.Length >= 5)
            {
                var url = parts[0].Replace("{{PIPE}}", "|");
                var title = parts[1].Replace("{{PIPE}}", "|");
                var description = parts[2].Replace("{{PIPE}}", "|");
                var imageUrl = parts[3].Replace("{{PIPE}}", "|");
                var siteName = parts[4].Replace("{{PIPE}}", "|");
                
                var previewElement = CreateLinkPreviewElement(url, title, description, imageUrl, siteName, textBrush);
                mainPanel.Children.Add(previewElement);
            }
        }
        
        return mainPanel;
    }
    
    private FrameworkElement CreateLinkPreviewElement(string url, string title, string description, string imageUrl, string siteName, Brush textBrush)
    {
        var accentBrush = Application.Current.Resources["AccentBrush"] as Brush ?? Brushes.Green;
        var cardBrush = Application.Current.Resources["CardBrush"] as Brush ?? new SolidColorBrush(Color.FromRgb(40, 40, 40));
        var secondaryBrush = Application.Current.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray;
        
        var border = new Border
        {
            Background = cardBrush,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 8, 0, 0),
            MaxWidth = 380,
            Cursor = System.Windows.Input.Cursors.Hand,
            ClipToBounds = true
        };
        
        var mainStack = new StackPanel();
        
        // Картинка (если есть)
        if (!string.IsNullOrEmpty(imageUrl))
        {
            var imageContainer = new Border
            {
                Height = 180,
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };
            
            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Загружаем картинку асинхронно
            LoadPreviewImageAsync(image, imageUrl);
            
            imageContainer.Child = image;
            mainStack.Children.Add(imageContainer);
        }
        
        // Текстовый контент
        var contentBorder = new Border
        {
            Padding = new Thickness(12, 10, 12, 10)
        };
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        // Вертикальная линия
        var line = new Border
        {
            Background = accentBrush,
            CornerRadius = new CornerRadius(2)
        };
        Grid.SetColumn(line, 0);
        grid.Children.Add(line);
        
        // Контент
        var content = new StackPanel();
        Grid.SetColumn(content, 2);
        
        content.Children.Add(new TextBlock
        {
            Text = siteName,
            FontSize = 11,
            Foreground = accentBrush,
            FontWeight = FontWeights.Medium
        });
        
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });
        
        if (!string.IsNullOrEmpty(description))
        {
            content.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = secondaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        
        grid.Children.Add(content);
        contentBorder.Child = grid;
        mainStack.Children.Add(contentBorder);
        
        border.Child = mainStack;
        
        // Клик открывает ссылку
        border.MouseLeftButtonUp += (s, e) => OpenUrl(url);
        
        return border;
    }
    
    private async void LoadPreviewImageAsync(Image imageControl, string imageUrl)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 ICYOU/1.0");
            
            var response = await httpClient.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageData = await response.Content.ReadAsByteArrayAsync();
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(imageData);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        
                        imageControl.Source = bitmap;
                    }
                    catch { }
                });
            }
        }
        catch { }
    }
    
    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
    
    private Button CreateControlButton(string content)
    {
        return new Button
        {
            Content = content,
            Width = 30,
            Height = 25,
            Margin = new Thickness(2),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
    }
    
    private void SaveFileWithPath(string fileName, byte[] data, string? savedPath)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = fileName, Filter = "Все файлы (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            // Если файл уже сохранён в Downloads - копируем его
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                File.Copy(savedPath, dialog.FileName, true);
            }
            else
            {
                File.WriteAllBytes(dialog.FileName, data);
            }
        }
    }
    
    private string FormatSize(long bytes)
    {
        string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }
    
    public static void PauseAllVideos()
    {
        foreach (var weakRef in _activeVideos.ToList())
        {
            if (weakRef.TryGetTarget(out var media)) media.Pause();
            else _activeVideos.Remove(weakRef);
        }
    }
}

