using System;
using System.Net;
using Gtk;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.DataAccess;
using XDMApp = XDM.Core.Application;
using System.Linq;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Util;

namespace XDM.GtkUI
{
    class Program
    {
        private const string DisableCachingName = @"TestSwitch.LocalAppContext.DisableCaching";
        private const string DontEnableSchUseStrongCryptoName = @"Switch.System.Net.DontEnableSchUseStrongCrypto";

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Load configuration from macOS standard location
                var macOSAppDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "XDM"
                );

                // Initialize config with LoadConfig to load persisted settings
                Config.LoadConfig();

                // Override with macOS-standard directories (must be done after LoadConfig)
                Config.AppDir = macOSAppDir;
                Config.DataDir = System.IO.Path.Combine(macOSAppDir, "Data");

                // Setup debug logging if enabled
                var debugMode = Environment.GetEnvironmentVariable("XDM_DEBUG_MODE");
                if (!string.IsNullOrEmpty(debugMode) && debugMode == "1")
                {
                    var logFile = System.IO.Path.Combine(Config.AppDir, "log.txt");
                    Log.InitFileBasedTrace(logFile);
                }
                Log.Debug("Application_Startup");

                // Initialize GTK application
                Gtk.Application.Init("xdm-app", ref args);
                GLib.ExceptionManager.UnhandledException += ExceptionManager_UnhandledException;

                // Setup global CSS styling
                var globalStyleSheet = @"
                                    .large-font{ font-size: 16px; }
                                    .medium-font{ font-size: 14px; }
                                    ";

                var screen = Gdk.Screen.Default;
                var provider = new CssProvider();
                provider.LoadFromData(globalStyleSheet);
                Gtk.StyleContext.AddProviderForScreen(screen, provider, 800);

                // Configure network settings
                ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
                ServicePointManager.DefaultConnectionLimit = 100;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

                // AppContext configuration for compatibility
                AppContext.SetSwitch(DisableCachingName, true);
                AppContext.SetSwitch(DontEnableSchUseStrongCryptoName, true);

                Log.Debug("Loading languages...");
                LoadLanguageTexts();

                // Setup dark theme if enabled
                if (Config.Instance.AllowSystemDarkTheme)
                {
                    Gtk.Settings.Default.ThemeName = "Adwaita";
                    Gtk.Settings.Default.ApplicationPreferDarkTheme = true;
                }

                // Initialize core application components
                var core = new ApplicationCore();
                var app = new XDMApp();
                var win = new MainWindow();

                Log.Debug("Configuring app context...");

                // Register application components
                ApplicationContext.FirstRunCallback += ApplicationContext_FirstRunCallback;
                ApplicationContext.Configurer()
                    .RegisterApplicationWindow(win)
                    .RegisterApplication(app)
                    .RegisterApplicationCore(core)
                    .RegisterCapturedVideoTracker(new VideoTracker())
                    .RegisterClipboardMonitor(new ClipboardMonitor())
                    .RegisterLinkRefresher(new LinkRefresher())
                    .RegisterPlatformUIService(new GtkPlatformUIService())
                    .Configure();

                Log.Debug("Processing arguments...");
                ArgsProcessor.Process(args);

                Log.Debug("Gtk Run...");
                Gtk.Application.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        private static void ApplicationContext_FirstRunCallback(object? sender, EventArgs e)
        {
            PlatformHelper.EnableAutoStart(true);
        }

        private static void ExceptionManager_UnhandledException(GLib.UnhandledExceptionArgs args)
        {
            Log.Debug("GLib ExceptionManager_UnhandledException: " + args.ExceptionObject);
            args.ExitApplication = false;
        }

        private static void LoadLanguageTexts()
        {
            Log.Debug("Language loading ...");
            try
            {
                var indexFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Lang/index.txt");
                if (System.IO.File.Exists(indexFile))
                {
                    var lines = System.IO.File.ReadAllLines(indexFile);
                    foreach (var line in lines)
                    {
                        var index = line.IndexOf("=");
                        if (index > 0)
                        {
                            var name = line.Substring(0, index);
                            var value = line.Substring(index + 1);
                            if (name == Config.Instance.Language)
                            {
                                TextResource.Load(value);
                                break;
                            }
                        }
                    }
                }
                Log.Debug("Language loaded.");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }
    }
}
