using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;

namespace HuionResizer
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow {
        bool drag = false;
        Point startPoint;

        private float tabWidth;
        private float tabHeight;
        private float screenWidth;
        private float screenHeight;
        private float startWidth;
        private float startHeight;
        private float left, right, top, bottom;
        private double width = 0;
        private double height = 0;
        private Configuration config;
        public float mul = 100;
        private bool dontUpdate = false;

        private void DragMouseDown(object sender, MouseButtonEventArgs e)
        {
            drag = true;
            startPoint = Mouse.GetPosition(BackPanel);
        }

        private void DragMouseMove(object sender, MouseEventArgs e)
        {
            try {
                if (drag) {
                    Rectangle draggedRectangle = sender as Rectangle;
                    Point newPoint = Mouse.GetPosition(BackPanel);
                    double left = Canvas.GetLeft(draggedRectangle);
                    double top = Canvas.GetTop(draggedRectangle);

                    if (top + draggedRectangle.Height > BackPanel.Height) {
                        Canvas.SetTop(draggedRectangle, BackPanel.Height - draggedRectangle.Height);
                    } else if (top < 0) {
                        Canvas.SetTop(draggedRectangle, 0);
                    } else {
                        Canvas.SetTop(draggedRectangle, top + (newPoint.Y - startPoint.Y));
                    }

                    if (left + draggedRectangle.Width > BackPanel.Width) {
                        Canvas.SetLeft(draggedRectangle, BackPanel.Width - draggedRectangle.Width);
                    } else if (left < 0) {
                        Canvas.SetLeft(draggedRectangle, 0);
                    } else {
                        Canvas.SetLeft(draggedRectangle, left + (newPoint.X - startPoint.X));
                    }
                    startPoint = newPoint;

                    // Update textbox value
                    StartingWidth.Text = Math.Round((Canvas.GetLeft(draggedRectangle) * (screenWidth / width))).ToString();
                    StartingHeight.Text = Math.Round((Canvas.GetTop(draggedRectangle) * (screenHeight / height))).ToString();
                }
            } catch (Exception ex) {
                this.ShowMessageAsync("Errore",ex.Message);
            }

        }
        private void DragMouseUp(object sender, MouseButtonEventArgs e)
        {
            drag = false;
            GenerateSilent();
        }

        public void UpdateRect(object sender, RoutedEventArgs e) {
            UpdateRectPrivate();
        }

        public void UpdateRectPrivate() {
            const double fixedWidth = 500;
            if (!GenerateSilent()) {
                return;
            }

            width = fixedWidth;
            height = width * (tabHeight / tabWidth);
            BackPanel.Width = width;
            BackPanel.Height = height;
            Canvas.SetLeft(SelectionRectangle, width * left);
            Canvas.SetTop(SelectionRectangle, height * top);
            SelectionRectangle.Width = width * (right - left);
            SelectionRectangle.Height = height * (bottom - top);
        }

        private bool UpdateValue() {
            mul = (float) AspectRatio.Value;
            return (float.TryParse(TabletWidth.Text, out tabWidth) &&
                    float.TryParse(TabletHeight.Text, out tabHeight) &&
                    float.TryParse(ScreenWidth.Text, out screenWidth) &&
                    float.TryParse(ScreenHeight.Text, out screenHeight) &&
                    float.TryParse(StartingWidth.Text, out startWidth) &&
                    float.TryParse(StartingHeight.Text, out startHeight));
        }

        private bool GenerateSilent() {
            if (!this.IsLoaded || drag || dontUpdate) {
                return false;
            }
            if (UpdateValue()) {
                if (tabWidth > 0 &&
                    tabHeight > 0 &&
                    screenWidth > 0 &&
                    screenHeight > 0 &&
                    startWidth >= 0 && startWidth < screenWidth &&
                    startHeight >= 0 && startHeight < screenHeight &&
                    mul > 0 && mul <= 100) {

                    // Calculate Aspect ratio
                    var tabRatio = tabWidth / tabHeight;
                    var screenRatio = screenWidth / screenHeight;

                    // Calculate max size
                    if (screenRatio < tabRatio) {
                        bottom = 1.0f;
                        right = screenRatio / tabRatio;
                    } else {
                        right = 1.0f;
                        bottom = tabRatio / screenRatio;
                    }

                    // Calculate dimension
                    right *= (mul / 100);
                    bottom *= (mul / 100);

                    // Calculate offset
                    right += startWidth / screenWidth;
                    bottom += startHeight / screenHeight;
                    left = startWidth / screenWidth;
                    top = startHeight / screenHeight;


                    // Check range and fix mul
                    if (right > 1.0f) {
                        var excess = right - 1.0f;
                        right = 1.0f;
                        bottom -= excess * (1.0f / screenRatio);
                    }

                    if (bottom > 1.0f) {
                        var excess = bottom - 1.0f;
                        bottom = 1.0f;
                        right -= excess * screenRatio;
                    }

                    // Update mul boxes
                    BoxAspectRatio.Text = Math.Truncate(mul).ToString();
                    AspectRatio.Value = mul;


                    var result = string.Format(
                        "WorkAreaRatio_Left={0:0.000000}\nWorkAreaRatio_Right={1:0.000000}\nWorkAreaRatio_Top={2:0.000000}\nWorkAreaRatio_Bottom={3:0.000000}",
                        left, right, top, bottom);

                    ResultBox.Text = result;
                    return true;
                } else {
                    this.ShowMessageAsync("I valori inseriti non sono validi.", "Valori non validi");
                    return false;
                }
            } else {
                this.ShowMessageAsync("I valori inseriti nei non sono numeri.", "Valori non validi");
                return false;
            }
        }

        private void GenerateClick(object sender, RoutedEventArgs e) {
            GenerateSilent();

            // Write to settings
            // Get path
            var path = config.AppSettings.Settings["Path"].Value;
            var exePath = config.AppSettings.Settings["ExePath"].Value;
            var autoRestartApp = config.AppSettings.Settings["AutoRestartApp"].Value == "1";
            var processName = config.AppSettings.Settings["ProcessName"].Value;
            Console.WriteLine($"Nome: {0}",path);
            if (path == "" || ((exePath == "" || processName == "") && autoRestartApp)) {
                this.ShowMessageAsync("Impostazioni non valide", "Non sono state impostate correttamente le impostazioni!");
                SettingsFlyout.IsOpen = true;
                return;
            }

            string[] tabConfig = File.ReadAllLines(path);

            for (var i = 0; i < tabConfig.Length; i++) {
                var line = tabConfig[i];
                if (Regex.Match(line, "^WorkAreaRatio_Left=.*$").Success) {
                    tabConfig[i] = $"WorkAreaRatio_Left={left:0.000000}";
                }
                if (Regex.Match(line, "^WorkAreaRatio_Right=.*$").Success) {
                    tabConfig[i] = $"WorkAreaRatio_Right={right:0.000000}";
                }
                if (Regex.Match(line, "^WorkAreaRatio_Top=.*$").Success) {
                    tabConfig[i] = $"WorkAreaRatio_Top={top:0.000000}";
                }
                if (Regex.Match(line, "^WorkAreaRatio_Bottom=.*$").Success) {
                    tabConfig[i] = $"WorkAreaRatio_Bottom={bottom:0.000000}";
                }
            }
            if (autoRestartApp) {
                try {
                    Process[] proc = Process.GetProcessesByName(processName);
                    if (proc.Length > 0) {
                        proc[0].Kill();
                    }
                }
                catch (Exception ex) {
                    this.ShowMessageAsync("Errore",ex.Message);
                }
            }

            File.WriteAllLines(path, tabConfig);

            if (autoRestartApp) {
                // var dir = Directory.GetParent(path);
                try {
                    Process.Start(exePath);
                } catch (Exception ex) {
                    this.ShowMessageAsync("Errore",ex.Message);
                }
            }

            this.ShowMessageAsync("Scrittura completata!", "Operazione completata");
        }

        private string setPath()
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set name
            openFileDialog1.Title = "Seleziona il file di configurazione:";

            // Set filter options and filter index.
            openFileDialog1.Filter = "Tablet Config (tabletconfig.ini)|tabletconfig.ini|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == true) {
                config.AppSettings.Settings["Path"].Value = openFileDialog1.FileName;
                SaveConfigChanges();
                return openFileDialog1.FileName;
            }
            else {
                return null;
            }

        }

        private void WindowLoaded(object sender, RoutedEventArgs e) {
            UpdateRectPrivate();
        }

        private void openSettings(object sender, RoutedEventArgs e) {
            /*
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
            */
            SettingsFlyout.IsOpen = true;
        }

        private void openInfos(object sender, RoutedEventArgs e) {
            this.ShowMessageAsync("Informazioni sull'autore", "Huion Resizer v1.0\nCreato da Fuji (fuji97)\n\nSito web: https://fujiblog.me \nGitHub repo: https://github.com/fuji97/huion-resizer");
        }

        private void AutoRestartApp_OnChecked(object sender, RoutedEventArgs e)
        {
            config.AppSettings.Settings["AutoRestartApp"].Value = "1";
            SaveConfigChanges();
        }

        private void AutoRestartApp_OnUnchecked(object sender, RoutedEventArgs e)
        {
            config.AppSettings.Settings["AutoRestartApp"].Value = "0";
            SaveConfigChanges();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set name
            openFileDialog1.Title = "Seleziona il file di configurazione:";

            // Set filter options and filter index.
            openFileDialog1.Filter = "Tablet Config (tabletconfig.ini)|tabletconfig.ini|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == true) {
                config.AppSettings.Settings["Path"].Value = openFileDialog1.FileName;
                SaveConfigChanges();
                ConfigPath.Text = openFileDialog1.FileName;
            }
        }

        private void ButtonExe_onClick(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set name
            openFileDialog1.Title = "Seleziona l'eseguibile:";

            // Set filter options and filter index.
            openFileDialog1.Filter = "Eseguibile Windows (*.exe)|*.exe|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == true) {
                config.AppSettings.Settings["ExePath"].Value = openFileDialog1.FileName;
                SaveConfigChanges();
                ExePath.Text = openFileDialog1.FileName;
            }
        }

        private void SaveConfigChanges() {
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public MainWindow() {
            try {
                config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
            catch (ConfigurationErrorsException e) {
                Console.WriteLine(e);
                MessageBox.Show("Non è presente il file di configurazione.\nNon è possibile avviare il programma.","Configurazione mancante", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            InitializeComponent();
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            ConfigPath.Text = config.AppSettings.Settings["Path"].Value;
            ExePath.Text = config.AppSettings.Settings["ExePath"].Value;
            ProcessName.Text = config.AppSettings.Settings["ProcessName"].Value;
            AutoRestartApp.IsChecked = ConfigurationManager.AppSettings.Get("AutoRestartApp") == "1";
        }

        private void ProcessName_OnTextChanged(object sender, TextChangedEventArgs e) {
            config.AppSettings.Settings["ProcessName"].Value = ProcessName.Text;
            SaveConfigChanges();
        }

        private void ImportDriverValues_OnClick(object sender, RoutedEventArgs e) {
            var path = config.AppSettings.Settings["Path"].Value;

            if (path == "") {
                this.ShowMessageAsync("Percorso mancante",
                    "Il percorso per il file di configurazione dei driver non è stato impostato nelle impostazioni.");
                SettingsFlyout.IsOpen = true;
                return;
            }
            string file;
            try {
                file = File.ReadAllText(path);
            }
            catch (Exception exception) {
                Console.WriteLine(exception);
                this.ShowMessageAsync("Impossibile aprire il file",
                    "Il percorso impostato potrebbe non essere valido o il file potrebbe essere impossibile da aprire.");
                return;
            }
            
            Console.WriteLine("Finding values...");
            var match = Regex.Match(file, @"^WorkAreaRatio_Left=(\d*\.\d*)", RegexOptions.Multiline);
            if (match.Success) {
                left = float.Parse(match.Groups[1].Value);
                Console.WriteLine($"Left: {0} ({1})", left, match.Groups[1].Captures[0].Value);
            }
            match = Regex.Match(file, @"^WorkAreaRatio_Right=(\d*\.\d*)", RegexOptions.Multiline);
            if (match.Success) {
                right = float.Parse(match.Groups[1].Captures[0].Value);
                Console.WriteLine($"Right: {0} ({1})", right, match.Groups[1]);
            }
            match = Regex.Match(file, @"^WorkAreaRatio_Top=(\d*\.\d*)", RegexOptions.Multiline);
            if (match.Success) {
                top = float.Parse(match.Groups[1].Captures[0].Value);
                Console.WriteLine($"Top: {0} ({1})", top, match.Groups[1].Captures[0].Value);
            }
            match = Regex.Match(file, @"^WorkAreaRatio_Bottom=(\d*\.\d*)", RegexOptions.Multiline);
            if (match.Success) {
                bottom = float.Parse(match.Groups[1].Captures[0].Value);
                Console.WriteLine($"Bottom: {0} ({1})", bottom, match.Groups[1].Captures[0].Value);
            }

            var tabRatio = tabWidth / tabHeight;
            var screenRatio = screenWidth / screenHeight;
            var lWidth = right - left;
            var lHeight = bottom - top;
            var relRatio = screenRatio / tabRatio;
            var lRatio = lWidth / lHeight;

            Console.WriteLine($"Check {0:0.00000000} == {1:0.00000000}", lRatio, relRatio);
            if (Math.Abs(lRatio - relRatio) > 0.0001) {
                this.ShowMessageAsync("Proporzioni diverse",
                    "Le proporzioni dello schermo impostate sono diverse da quelle dei driver.");
                return;
            }

            if (relRatio >= 1) {
                mul = lWidth * 100;
            }
            else {
                mul = lHeight * 100;
            }

            startWidth = left * screenWidth;
            startHeight = top * screenHeight;

            dontUpdate = true;
            StartingWidth.Text = startWidth.ToString(CultureInfo.InvariantCulture);
            StartingHeight.Text = startHeight.ToString(CultureInfo.InvariantCulture);
            AspectRatio.Value = mul;
            dontUpdate = false;

            GenerateSilent();
        }
    }
}
