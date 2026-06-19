using HandheldCompanion.Helpers;
using HandheldCompanion.Shared;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.UI.Notifications;

namespace HandheldCompanion.Managers
{
    /// <summary>
    /// Strongly-typed action attached to a toast button.
    /// You can either:
    ///  - provide a Command + Parameters (durable; survives app re-activation), or
    ///  - provide a Callback (in-process; works while this instance is alive).
    /// </summary>
    public sealed class ToastAction
    {
        /// <summary>Button label.</summary>
        public string Label { get; set; } = "";

        /// <summary>Optional local image (png) for the button (absolute or file:/// URI). Windows toast buttons do not support font glyphs; use a small PNG if you need an icon.</summary>
        public string? IconPath { get; set; }

        /// <summary>Durable command name (e.g. "SetTarget"). Recommended if the action should work even after app reactivation.</summary>
        public string? Command { get; set; }

        /// <summary>Durable parameters (e.g. { deviceId = "...", powerCycle = "false" }).</summary>
        public Dictionary<string, string>? Parameters { get; set; }

        /// <summary>Optional in-process callback. Runs on the UI dispatcher if available.</summary>
        public Action<IReadOnlyDictionary<string, string>>? Callback { get; set; }

        internal string EphemeralId { get; set; } = ""; // filled by manager
    }

    public static class ToastIconHelper
    {
        // Renders a glyph to a tightly filled square PNG (ideal for toast buttons).
        public static string RenderGlyphPng(string glyph, string outputPath, int px = 32, string fontFamilyName = "Segoe Fluent Icons", Color? foreground = null)
        {
            if (string.IsNullOrEmpty(glyph))
                return glyph;

            Color color = foreground ?? Colors.White;

            Typeface tf = new Typeface(new FontFamily(fontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!tf.TryGetGlyphTypeface(out GlyphTypeface? gtf))
                return string.Empty;

            // Map char -> glyph index
            char ch = glyph[0];
            if (!gtf.CharacterToGlyphMap.TryGetValue(ch, out var gix))
                return string.Empty;

            // Build at large EM and scale-to-fit box
            const double em = 1000.0;
            double[] advances = new[] { gtf.AdvanceWidths[gix] * em };
            GlyphRun run = new GlyphRun(gtf, 0, false, em, new ushort[] { (ushort)gix }, new Point(0, 0), advances, null, null, null, null, null, null);
            Geometry geo = run.BuildGeometry();
            Rect b = geo.Bounds;

            double scale = Math.Min(px / b.Width, px / b.Height);

            TransformGroup tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(scale, scale));
            tg.Children.Add(new TranslateTransform(-b.X * scale + (px - b.Width * scale) / 2.0, -b.Y * scale + (px - b.Height * scale) / 2.0));
            geo.Transform = tg;

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
                dc.DrawGeometry(new SolidColorBrush(color), null, geo);

            RenderTargetBitmap bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using FileStream fs = File.Create(outputPath);
            enc.Save(fs);
            return outputPath;
        }
    }

    /// <summary>A single item in a toast selection combobox.</summary>
    public sealed class ToastComboItem
    {
        public string Id { get; set; } = "";
        public string Content { get; set; } = "";

        public ToastComboItem() { }
        public ToastComboItem(string id, string content) { Id = id; Content = content; }
    }

    /// <summary>A selection combobox to embed in a toast notification.</summary>
    public sealed class ToastComboInput
    {
        /// <summary>Unique element id; also the key in the UserInput dictionary on activation.</summary>
        public string Id { get; set; } = "";
        /// <summary>Optional label shown above the combobox.</summary>
        public string Title { get; set; } = "";
        /// <summary>Id of the pre-selected item.</summary>
        public string DefaultSelectionBoxItemId { get; set; } = "";
        public List<ToastComboItem> Items { get; set; } = new();
    }

    /// <summary>Typed toast request with optional actions.</summary>
    public sealed class ToastRequest
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Content2 { get; set; } = "";
        public string Img { get; set; } = "icon";
        public bool IsHero { get; set; }
        public List<ToastAction> Actions { get; set; } = new();
        /// <summary>If true, shows toast as 'Important' scenario (longer duration, higher priority).</summary>
        public bool Important { get; set; }
        /// <summary>Optional manager name (e.g., "ProfileManager", "ControllerManager") to group toasts by source; enables concurrent notifications.</summary>
        public string? Manager { get; set; }
        /// <summary>Optional custom tag/group if you want multiple toasts; by default we reuse the single-tag replacement behavior.</summary>
        public string? Tag { get; set; }
        public string? Group { get; set; }
        /// <summary>Optional selection comboboxes shown above the action buttons.</summary>
        public List<ToastComboInput> SelectionBoxes { get; set; } = new();
    }

    public static class ToastManager
    {
        private const string DefaultGroup = "HandheldCompanion";
        private const string DefaultToastTag = "LatestToast";

        private static readonly ConcurrentQueue<ToastRequest> ToastQueue = new();
        private static readonly SemaphoreSlim _queueSemaphore = new SemaphoreSlim(1, 1);

        private static ToastNotification? CurrentToastNotification;

        public static bool IsEnabled => ManagerFactory.settingsManager.GetBoolean("ToastEnable");

        private static bool IsInitialized { get; set; }

        // Ephemeral action registry: token -> callback
        private static readonly ConcurrentDictionary<string, Action<IReadOnlyDictionary<string, string>>> ActionRegistry = new();

        // Manager-based toast registry: manager name -> last sent request (for force-redisplay)
        private static readonly ConcurrentDictionary<string, ToastRequest> ManagerToastRegistry = new();

        /// <summary>
        /// Raised when a toast button is clicked and carries a durable command.
        /// Args:
        ///  - command string (e.g., "SetTarget")
        ///  - all parsed arguments (includes your Parameters and system keys like "aid")
        /// </summary>
        public static event Action<string, IReadOnlyDictionary<string, string>>? CommandReceived;

        static ToastManager()
        {
            // Hook once to activation (foreground or background). This is the compat hook provided by the Toolkit.
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        }

        /// <summary>
        /// Simple overload preserved for existing callers.
        /// Replaces any currently showing toast (same Tag+Group behavior).
        /// </summary>
        public static bool SendToast(string title, string content = "", string img = "icon", bool isHero = false)
        {
            var request = new ToastRequest
            {
                Title = title,
                Content = content,
                Img = img,
                Important = false,
                IsHero = isHero,
                Tag = DefaultToastTag,
                Group = DefaultGroup
            };

            return SendToast(request);
        }

        /// <summary>
        /// New, typed API: send a toast with optional action buttons.
        /// For default Tag+Group (no Manager): displays immediately, replacing any current toast (single-instance).
        /// For Manager-based grouping: displays alongside other manager groups (concurrent notifications).
        /// For custom Tag+Group: queues independently for sequential display.
        /// </summary>
        public static bool SendToast(ToastRequest request)
        {
            if (!IsEnabled)
                return false;

            // If Manager is specified, use manager-based Tag/Group for concurrent notifications
            if (!string.IsNullOrWhiteSpace(request.Manager))
            {
                request.Tag = request.Manager;
                request.Group = request.Manager;

                // Store in manager registry for potential force-redisplay
                ManagerToastRegistry[request.Manager] = request;

                // Don't flush queue for manager-based toasts; let them queue independently
                ToastQueue.Enqueue(request);
                _ = ProcessToastQueue();
                return true;
            }

            // Default to single-instance behavior (old behavior)
            request.Tag ??= DefaultToastTag;
            request.Group ??= DefaultGroup;

            // Flush any pending items (single-instance only):
            while (ToastQueue.TryDequeue(out _)) { }

            // Remove previous toast from Action Center (same Tag+Group)
            if (CurrentToastNotification != null)
            {
                try { ToastNotificationManager.History.Remove(request.Tag, request.Group); }
                catch { /* ignore */ }
                finally { CurrentToastNotification = null; }
            }

            ToastQueue.Enqueue(request);
            _ = ProcessToastQueue();

            return true;
        }

        /// <summary>
        /// Force-redisplay a manager's last sent toast, bumping it to the front of the Action Center.
        /// Useful for re-showing alerts (e.g., controller slot issues) when related events occur (e.g., profile applied).
        /// </summary>
        public static bool ForceRedisplayManagerToast(string managerName)
        {
            return ForceRedisplayManagerToast(managerName, delayMs: 0);
        }

        /// <summary>
        /// Force-redisplay a manager's last sent toast with optional delay for staggered display.
        /// Delays help show multiple toasts sequentially so both are briefly visible on screen.
        /// </summary>
        /// <param name="managerName">The manager name to redisplay.</param>
        /// <param name="delayMs">Delay in milliseconds before displaying (default 0). Suggested: 500-800ms for sequential display.</param>
        public static bool ForceRedisplayManagerToast(string managerName, int delayMs = 0)
        {
            if (string.IsNullOrWhiteSpace(managerName) || !ManagerToastRegistry.TryGetValue(managerName, out var storedRequest))
                return false;

            if (delayMs > 0)
            {
                // Delay redisplay to allow previous toast to remain visible
                _ = Task.Delay(delayMs).ContinueWith(_ =>
                {
                    ToastQueue.Enqueue(storedRequest);
                    _ = ProcessToastQueue();
                });
            }
            else
            {
                // Re-enqueue the stored request to trigger a fresh display
                ToastQueue.Enqueue(storedRequest);
                _ = ProcessToastQueue();
            }

            return true;
        }

        private static async Task ProcessToastQueue()
        {
            // If already processing, don't start another loop (the existing one will drain everything)
            if (!_queueSemaphore.Wait(0)) // Non-blocking check
                return;

            try
            {
                while (ToastQueue.TryDequeue(out var toast))
                {
                    await UIHelper.TryInvokeAsync(() => DisplayToast(toast), DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                // Log but never crash because of a toast
                LogManager.LogError("Toast processing error: {0}", ex.Message);
            }
            finally
            {
                _queueSemaphore.Release();

                // If new items were added while we were releasing, process them
                if (!ToastQueue.IsEmpty && _queueSemaphore.Wait(0))
                {
                    _queueSemaphore.Release();
                    _ = ProcessToastQueue();
                }
            }
        }

        private static void DisplayToast(ToastRequest request)
        {
            // Build image URI if present
            Uri? imageUri = null;
            string imagePath;

            // Check if the image is a full file path (e.g., from library cache) or a resource name
            if (Path.IsPathRooted(request.Img) || File.Exists(request.Img))
            {
                // It's already a full file path
                imagePath = request.Img;
            }
            else
            {
                // It's a resource name, construct the full path
                imagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Resources\\{request.Img}.png";
            }

            if (File.Exists(imagePath))
                imageUri = new Uri($"file:///{imagePath}");

            // Unique id to correlate all actions from this toast instance
            string toastId = Guid.NewGuid().ToString("N");

            var builder = new ToastContentBuilder()
                .AddText(request.Title)
                .AddText(request.Content)
                .AddText(request.Content2);
            //.SetToastScenario(request.Important ? ToastScenario.Reminder : ToastScenario.Default);

            if (imageUri != null)
            {
                if (request.IsHero) builder.AddHeroImage(imageUri);
                else builder.AddAppLogoOverride(imageUri, ToastGenericAppLogoCrop.Default);
            }

            // Selection comboboxes
            foreach (var combo in request.SelectionBoxes ?? Enumerable.Empty<ToastComboInput>())
            {
                var box = new ToastSelectionBox(combo.Id)
                {
                    Title = combo.Title,
                    DefaultSelectionBoxItemId = combo.DefaultSelectionBoxItemId
                };
                foreach (var item in combo.Items)
                    box.Items.Add(new ToastSelectionBoxItem(item.Id, item.Content));
                builder.AddToastInput(box);
            }

            // Buttons
            foreach (var action in request.Actions ?? Enumerable.Empty<ToastAction>())
            {
                // ephemeral token for callback
                action.EphemeralId = Guid.NewGuid().ToString("N");

                // Register in-process callback if provided
                if (action.Callback is not null)
                    ActionRegistry[action.EphemeralId] = action.Callback;

                var btn = new ToastButton()
                    .SetContent(action.Label)
                    .AddArgument("aid", action.EphemeralId)   // ephemeral handle
                    .AddArgument("tid", toastId)              // toast id
                    .SetBackgroundActivation();               // avoid stealing focus

                // durable command + params (if provided)
                if (!string.IsNullOrWhiteSpace(action.Command))
                    btn.AddArgument("cmd", action.Command);

                if (action.Parameters is not null)
                {
                    foreach (var kv in action.Parameters)
                        btn.AddArgument(kv.Key, kv.Value ?? "");
                }

                // optional small icon
                if (!string.IsNullOrWhiteSpace(action.IconPath))
                {
                    // allow raw file path; fix up to file:/// if needed
                    var icon = action.IconPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                        ? new Uri(action.IconPath)
                        : new Uri("file:///" + action.IconPath.TrimStart('\\', '/').Replace('\\', '/'));
                    btn.SetImageUri(icon);
                }

                builder.AddButton(btn);
            }

            // Show toast with Tag/Group and keep a ref so we can clear it later
            builder.Show(toastNotification =>
            {
                toastNotification.Tag = request.Tag;
                toastNotification.Group = request.Group;
                CurrentToastNotification = toastNotification;
            });
        }

        private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                // Parse all arguments (Toolkit helper)
                var args = ToastArguments.Parse(e.Argument);

                // Convert to plain dictionary for ease of use and thread marshalling
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in args)
                    dict[kv.Key] = kv.Value;

                // Merge selection-box values from UserInput (keyed by element id)
                if (e.UserInput is not null)
                {
                    foreach (var kv in e.UserInput)
                    {
                        if (kv.Value is string sv)
                            dict[kv.Key] = sv;
                    }
                }

                // Otherwise use durable command route
                if (dict.TryGetValue("cmd", out var cmd) && !string.IsNullOrWhiteSpace(cmd))
                    CommandReceived?.Invoke(cmd!, dict);
            }
            catch { /* ignore */ }
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            LogManager.LogInformation("{0} has started", nameof(ToastManager));
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // Clear any queued toasts
            ToastQueue.Clear();

            // Remove current toast (from screen + Action Center)
            if (CurrentToastNotification != null)
            {
                try { ToastNotificationManager.History.Remove(DefaultToastTag, DefaultGroup); }
                catch { /* ignore */ }
                finally { CurrentToastNotification = null; }
            }

            // Clear ephemeral callbacks
            ActionRegistry.Clear();

            IsInitialized = false;
            LogManager.LogInformation("{0} has stopped", nameof(ToastManager));
        }
    }
}