using CoroutinesDotNet;
using CoroutinesForWpf;
using MarcosTomaz.ATS;
using Minecraft_Plus.Controls.ListItems;
using Minecraft_Plus.Scripts;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;

namespace Minecraft_Plus
{
    /*
     * This is the script responsible by the window of the launcher
    */

    public partial class MainWindow : Window
    {
        //Public enums
        public enum LauncherState
        {
            Normal,
            SystemTray
        }
        public enum MusicState
        {
            Playing,
            Mutted
        }

        //Public classes
        public class BackedUpInstanceFolder
        {
            public string folderOriginalPath = "";
            public string folderNewPath = "";
        }
        public class BackedUpInstanceFile
        {
            public string fileOriginalPath = "";
            public string fileNewPath = "";
        }

        //Cache variables
        private bool canCloseWindow = true;
        private bool isPlayingGame = false;
        private MediaPlayer musicMediaPlayer = null;
        private bool isMusicPlaying = false;
        private IDisposable musicPlayRoutine = null;
        private InstancesCatalog gameInstancesCatalog = null;
        private IDisposable instancesCatalogListUpdateRoutine = null;
        private List<InstanceItem> instantiatedInstanceItems = new List<InstanceItem>();
        private bool isInstanceSelectorDropdownOpen = false;
        private IDisposable openInstanceSelectorDropdownRoutine = null;
        private IDisposable closeInstanceSelectorDropdownRoutine = null;
        private ContextMenu playButtonMoreOptionsMenu = null;
        private MenuItem playContextMenuTitle = new MenuItem();
        private MenuItem smartUpdaterUpdateButton = new MenuItem();

        //Private variables
        private IDictionary<string, Storyboard> animStoryboards = new Dictionary<string, Storyboard>();
        private System.Windows.Forms.NotifyIcon launcherTrayIcon = null;
        private Preferences preferences = null;
        private string modpackPath = "";

        //Public variables
        public int currentSelectedGameInstanceId = -1;

        //Core methods

        public MainWindow()
        {
            //Check if have another process of the launcher already opened. If have, cancel this...
            string processName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 1)
            {
                //Warn about the problem
                MessageBox.Show("O Minecraft+ Launcher já está em execução!", "Erro");

                //Stop the execution of this instance
                System.Windows.Application.Current.Shutdown();

                //Cancel the execution
                return;
            }

            //Initialize the Window
            InitializeComponent();

            //Load references of animations of this window
            LoadAllStoryboardsAnimationsReferences();

            //Get the modpack path
            modpackPath = (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft-plus");

            //Prepare the UI
            PrepareTheUI();
        }

        private void LoadAllStoryboardsAnimationsReferences()
        {
            //Load references for all storyboards animations of this screen
            animStoryboards.Add("instanceSelectorDropdownOpen", (FindResource("instanceSelectorDropdownOpen") as Storyboard));
            animStoryboards.Add("instanceSelectorDropwdownClose", (FindResource("instanceSelectorDropwdownClose") as Storyboard));
        }

        private void PrepareTheUI()
        {
            //Block the window close
            this.Closing += (s, e) =>
            {
                if (canCloseWindow == false)
                {
                    MessageBox.Show("Aguarde a conclusão de todas as tarefas em andamento, antes de fechar o Minecraft+ Launcher.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    return;
                }
                if (isPlayingGame == true)
                {
                    SetLauncherState(LauncherState.SystemTray);
                    e.Cancel = true;
                    return;
                }
            };

            //Load the preferences
            preferences = new Preferences();

            //Create the modpack folder
            if (Directory.Exists(modpackPath) == false)
                Directory.CreateDirectory(modpackPath);

            //Reset the cache folder
            if (Directory.Exists((modpackPath + "/Cache")) == true)
                Directory.Delete((modpackPath + "/Cache"), true);

            //Create the folders structure
            if (Directory.Exists((modpackPath + "/Cache")) == false)
                Directory.CreateDirectory((modpackPath + "/Cache"));
            if (Directory.Exists((modpackPath + "/Downloads")) == false)
                Directory.CreateDirectory((modpackPath + "/Downloads"));
            if (Directory.Exists((modpackPath + "/Java")) == false)
                Directory.CreateDirectory((modpackPath + "/Java"));
            if (Directory.Exists((modpackPath + "/Launcher")) == false)
                Directory.CreateDirectory((modpackPath + "/Launcher"));
            if (Directory.Exists((modpackPath + "/Game")) == false)
                Directory.CreateDirectory((modpackPath + "/Game"));

            //
            //...
            //

            //Prepare the music
            if (preferences.loadedData.isMusicMuted == false)
                SetMusicState(MusicState.Playing);
            if (preferences.loadedData.isMusicMuted == true)
                SetMusicState(MusicState.Mutted);
            muteMusicBtn.Click += (s, e) =>
            {
                //Change the preferences of music
                preferences.loadedData.isMusicMuted = !preferences.loadedData.isMusicMuted;

                //Save the preferences
                preferences.Save();

                //Update the music
                if (preferences.loadedData.isMusicMuted == false)
                    SetMusicState(MusicState.Playing);
                if (preferences.loadedData.isMusicMuted == true)
                    SetMusicState(MusicState.Mutted);
            };

            //Prepare the video player
            instanceBgVideo.Balance = 0.0f;
            instanceBgVideo.IsMuted = true;
            instanceBgVideo.Volume = 0.0f;
            instanceBgVideo.Position = TimeSpan.Zero;
            instanceBgVideo.LoadedBehavior = MediaState.Manual;
            instanceBgVideo.SpeedRatio = 1.0f;
            instanceBgVideo.SpeedRatio = 1.0f;
            instanceBgVideo.Stretch = Stretch.UniformToFill;
            instanceBgVideo.StretchDirection = StretchDirection.Both;
            instanceBgVideo.UnloadedBehavior = MediaState.Stop;
            instanceBgVideo.MediaEnded += (s, e) =>
            {
                //Resplay the media, if have one
                if (instanceBgVideo.Source != null) 
                {
                    instanceBgVideo.Stop();
                    instanceBgVideo.Play();
                }
            };

            //Disable the status area
            statusArea.Visibility = Visibility.Collapsed;

            //Prepare the install button effects
            installBtn.MouseEnter += (s, e) => { installBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 128, 0)); };
            installBtn.MouseLeave += (s, e) => { installBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 212, 113, 13)); };
            installBtn.MouseLeftButtonUp += (s, e) => { InstallGameInstance(this.currentSelectedGameInstanceId); };
            //Prepare the play button effects
            playBtn.MouseEnter += (s, e) => { playBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 234, 34)); };
            playBtn.MouseLeave += (s, e) => { playBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 22, 167, 43)); };
            playBtn.MouseLeftButtonUp += (s, e) => { PlayGameInstance(this.currentSelectedGameInstanceId); };
            moreBtn.MouseEnter += (s, e) => { moreBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 234, 34)); };
            moreBtn.MouseLeave += (s, e) => { moreBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 22, 167, 43)); };
            moreBtn.MouseLeftButtonUp += (s, e) => { OpenMoreOptionsAboutGameInstance(s, e); };

            //Prepare the instance selector effects
            instanceSelectBg.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255));
            instanceSelectBg.MouseEnter += (s, e) => { instanceSelectBg.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 255, 255, 255)); };
            instanceSelectBg.MouseLeave += (s, e) => { instanceSelectBg.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255)); };
            instanceSelectBg.MouseLeftButtonUp += (s, e) => { ToggleInstanceSelectorDropdown(); };
            instanceSelectorDropDown.Visibility = Visibility.Collapsed;
            //Change the indicator arrow of the instance selector
            instanceArrowDown.Visibility = Visibility.Collapsed;
            instanceArrowUp.Visibility = Visibility.Visible;
            //Hide the warns
            firstRunWarn.Visibility = Visibility.Collapsed;
            updateWarn.Visibility = Visibility.Collapsed;

            //Prepare the edit skin button
            editNickBtn.Click += (s, e) => { OpenNicknameEditor(); };

            //Setup the links buttons
            gitHubBtn.Click += (s, e) => { System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = "https://github.com/marcos4503/minecraft-plus", UseShellExecute = true }); };
            donateBtn.Click += (s, e) => { System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = "https://www.paypal.com/donate/?hosted_button_id=MVDJY3AXLL8T2", UseShellExecute = true }); };
            editSkinBtn.Click += (s, e) => { System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = "https://www.minecraft.net/pt-br/msaprofile/mygames/editskin", UseShellExecute = true }); };

            //Show the launcher version
            this.Title = (this.Title + " - " + GetLauncherVersion());

            //Disable the screens
            downloadingGameDataScreen.Visibility = Visibility.Collapsed;
            launcherScreen.Visibility = Visibility.Collapsed;

            //If the base game data is not present, go to download
            if (File.Exists((modpackPath + @"/Game/local-current-version.txt")) == false || File.Exists((modpackPath + @"/Downloads/instances-catalog.json")) == false)
                DownloadBaseGameData();
            //If the base game data is present, go to start the launcher
            if (File.Exists((modpackPath + @"/Game/local-current-version.txt")) == true && File.Exists((modpackPath + @"/Downloads/instances-catalog.json")) == true)
                StartLauncher();
        }

        private void DownloadBaseGameData()
        {
            //Prepare to download base game data
            canCloseWindow = false;

            //Prepare the UI
            downloadingGameDataScreen.Visibility = Visibility.Visible;
            launcherScreen.Visibility = Visibility.Collapsed;
            downloadGameDataStatusTxt.Text = "Inicializando";
            downloadGameDataProgressBar.Value = 0.0f;
            downloadGameDataProgressBar.Maximum = 100.0f;
            downloadGameDataProgressBar.IsIndeterminate = true;

            //Start a thread to download the base game data
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Wait some time
                threadTools.MakeThreadSleep(3000);

                //Report the progress
                threadTools.ReportNewProgress("Preparando-I-0-0");

                //Wait some time
                threadTools.MakeThreadSleep(3000);

                //Try to do the task
                try
                {
                    //Report the progress
                    threadTools.ReportNewProgress("Baixando Catálogo de Versões-I-0-0");
                    threadTools.MakeThreadSleep(3000);

                    //Download instance catalog
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = @"https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/instance-catalog/catalog.json";
                        string saveAsPath = (modpackPath + @"/Cache/catalog.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //If the definitive catalog already exists, delete it
                    if (File.Exists((modpackPath + @"/Downloads/instances-catalog.json")) == true)
                        File.Delete((modpackPath + @"/Downloads/instances-catalog.json"));
                    //Move the catalog downloaded to the definitive path
                    File.Move((modpackPath + @"/Cache/catalog.json"), (modpackPath + @"/Downloads/instances-catalog.json"));

                    //Report the progress
                    threadTools.ReportNewProgress("Baixando Dados-I-0-0");
                    threadTools.MakeThreadSleep(3000);

                    //Download game data info
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = @"https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/game-data/current-game-data-info.json";
                        string saveAsPath = (modpackPath + @"/Cache/current-game-data-info.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //Parse the download info file
                    CurrentGameDataInfo currentGameDataInfo = new CurrentGameDataInfo((modpackPath + @"/Cache/current-game-data-info.json"));

                    //Report the progress
                    threadTools.ReportNewProgress("Baixando Dados-D-0-100");
                    threadTools.MakeThreadSleep(3000);

                    //Download game data
                    if (true == true)
                    {
                        //Prepare the list of downloaded files
                        List<string> downloadedFilesList = new List<string>();

                        //Download all zip parts
                        for (int i = 0; i < currentGameDataInfo.loadedData.downloads.Length; i++)
                        {
                            //Split download URL parts
                            string[] downloadUriParts = currentGameDataInfo.loadedData.downloads[i].Split("/");
                            //Prepare the save as path
                            string saveAsPath = (modpackPath + @"/Cache/" + downloadUriParts[downloadUriParts.Length - 1]);
                            //Download the current file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(currentGameDataInfo.loadedData.downloads[i]).Result;
                            httpRequestResult.EnsureSuccessStatusCode();
                            Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                            FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            downloadStream.CopyTo(fileStream);
                            httpClient.Dispose();
                            fileStream.Dispose();
                            fileStream.Close();
                            downloadStream.Dispose();
                            downloadStream.Close();

                            //Add downloaded file to list
                            downloadedFilesList.Add(saveAsPath);

                            //Inform the progress
                            threadTools.ReportNewProgress(("Baixando Dados-D-" + (i + 1) + "-" + currentGameDataInfo.loadedData.downloads.Length));
                        }

                        //Report the progress
                        threadTools.MakeThreadSleep(3000);
                        threadTools.ReportNewProgress("Instalando Dados-I-0-0");
                        threadTools.MakeThreadSleep(3000);

                        //Extract downloaded file
                        Process process = new Process();
                        process.StartInfo.FileName = System.IO.Path.Combine(modpackPath, "Launcher", "Resources", "7Zip", "7z.exe");
                        process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPath, "Launcher", "Resources", "7Zip");
                        process.StartInfo.Arguments = "x \"" + downloadedFilesList[0] + "\" -o\"" + (modpackPath + @"/Game") + "\" -y";  //<- Extract to "Game" overwriting existant
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        //Wait process finishes
                        process.WaitForExit();

                        //Store the new downloaded data version
                        File.WriteAllText((modpackPath + @"/Game/local-current-version.txt"), currentGameDataInfo.loadedData.version);
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);
                    threadTools.ReportNewProgress("Finalizando-I-0-0");
                    threadTools.MakeThreadSleep(3000);

                    //Open the "prismlauncher.cfg"
                    PrismLauncherCfgFile prismLauncherCfg = new PrismLauncherCfgFile((modpackPath + @"/Game/prismlauncher.cfg"));

                    //Update the settings
                    prismLauncherCfg.UpdateValue("IgnoreJavaWizard", "true");
                    prismLauncherCfg.UpdateValue("JavaPath", "");
                    prismLauncherCfg.UpdateValue("MaxMemAlloc", "8096");
                    prismLauncherCfg.UpdateValue("MinMemAlloc", "2048");
                    prismLauncherCfg.UpdateValue("PermGen", "512");
                    prismLauncherCfg.UpdateValue("CloseAfterLaunch", "true");
                    prismLauncherCfg.UpdateValue("QuitAfterGameStop", "true");
                    prismLauncherCfg.UpdateValue("DownloadsDir", (modpackPath.Replace("/", "\\") + @"\Downloads").Replace("\\", "/"));

                    //Save the "prismlauncher.cfg"
                    prismLauncherCfg.Save();

                    //Return a success response
                    return new string[] { "success" };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error" };
                }

                //Finish the thread...
                return new string[] { "none" };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //Split the new progress info
                string progressMessage = newProgress.Split("-")[0];
                bool isIndeterminate = (newProgress.Split("-")[1] == "I" ? true : false);
                int progressValue = int.Parse(newProgress.Split("-")[2]);
                int maxProgress = int.Parse(newProgress.Split("-")[3]);

                //Update the UI
                downloadGameDataStatusTxt.Text = progressMessage;
                if (isIndeterminate == false)
                {
                    downloadGameDataProgressBar.Value = progressValue;
                    downloadGameDataProgressBar.Maximum = maxProgress;
                    downloadGameDataProgressBar.IsIndeterminate = false;
                }
                if (isIndeterminate == true)
                {
                    downloadGameDataProgressBar.Value = 0.0f;
                    downloadGameDataProgressBar.Maximum = 100.0f;
                    downloadGameDataProgressBar.IsIndeterminate = true;
                }
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have a error, stop the launcher
                if (threadTaskResponse != "success")
                {
                    CloseLauncherWithError("Houve um problema ao obter os dados necessários para o jogo. Por favor, tente novamente mais tarde.");
                    return;
                }

                //If have sucess, continue to next step
                if (threadTaskResponse == "success")
                {
                    StartLauncher();
                }
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void StartLauncher()
        {
            //Allow to close the window
            canCloseWindow = true;

            //Prepare the UI
            downloadingGameDataScreen.Visibility = Visibility.Collapsed;
            launcherScreen.Visibility = Visibility.Visible;

            //Load the nickname and skin
            LoadNicknameAndSkin();

            //Update the instances catalog
            UpdateTheInstancesCatalog();
        }

        private void UpdateTheInstancesCatalog()
        {
            //Prepare to update de instances catalog
            canCloseWindow = false;

            //Change to corrent play button
            loadingButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            playButton.Visibility = Visibility.Collapsed;
            loadingButton.Visibility = Visibility.Visible;

            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Start a new thread to update the instances catalog
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Try to do the task
                try
                {
                    //Download instance catalog
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = @"https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/instance-catalog/catalog.json";
                        string saveAsPath = (modpackPath + @"/Cache/catalog.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //If the definitive catalog already exists, delete it
                    if (File.Exists((modpackPath + @"/Downloads/instances-catalog.json")) == true)
                        File.Delete((modpackPath + @"/Downloads/instances-catalog.json"));
                    //Move the catalog downloaded to the definitive path
                    File.Move((modpackPath + @"/Cache/catalog.json"), (modpackPath + @"/Downloads/instances-catalog.json"));

                    //Return a success response
                    return new string[] { "success" };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error" };
                }

                //Finish the thread...
                return new string[] { "none" };
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Render the instances catalog
                RenderTheInstancesCatalogOnSelector();
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void RenderTheInstancesCatalogOnSelector()
        {
            //Allow close window, again
            canCloseWindow = true;

            //Change to corrent play button
            loadingButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            playButton.Visibility = Visibility.Collapsed;
            loadingButton.Visibility = Visibility.Visible;

            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Load the instances catalog text
            string instancesCatalogText = File.ReadAllText((modpackPath + @"/Downloads/instances-catalog.json"));

            //Prepare the expected instances catalog version
            string exptdCatalogVersion = "1.0.0";
            //If the instances catalog is not the expected version, cancel here...
            if (instancesCatalogText.ToLower().Replace("\r\n", "").Replace("\n", "").Replace("\r", "").Replace(" ", "").Contains(("\"catalogversion\":\""+ exptdCatalogVersion + "\"")) == false)
            {
                //Warn about the problem, and quit
                canCloseWindow = true;
                MessageBox.Show("Não foi possível carregar o catálogo de versões. Por favor, inicie o Minecraft+ Launcher novamente!", "Erro");
                System.Windows.Application.Current.Shutdown();
                return;
            }

            //Parse the downloaded instances catalog
            gameInstancesCatalog = new InstancesCatalog((modpackPath + @"/Downloads/instances-catalog.json"));

            //Update the instances catalog on the selector
            UpdateInstancesCatalogSelectorList();
        }

        private void UpdateInstancesCatalogSelectorList()
        {
            //If the routine is already running, stop it
            if (instancesCatalogListUpdateRoutine != null)
            {
                instancesCatalogListUpdateRoutine.Dispose();
                instancesCatalogListUpdateRoutine = null;
            }

            //Start the update routine
            instancesCatalogListUpdateRoutine = Coroutine.Start(UpdateInstancesCatalogSelectorListRoutine());
        }

        private IEnumerator UpdateInstancesCatalogSelectorListRoutine()
        {
            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Wait some time
            yield return new WaitForSeconds(1.0f);

            //Clear all saves previously rendered
            foreach (InstanceItem item in instantiatedInstanceItems)
                instancesList.Children.Remove(item);
            instantiatedInstanceItems.Clear();

            //Render all instances items
            foreach (GameInstance instanceItem in gameInstancesCatalog.loadedData.availableInstances)
            {
                //Draw the item on screen
                InstanceItem newItem = new InstanceItem(this, modpackPath);
                instancesList.Children.Add(newItem);
                instantiatedInstanceItems.Add(newItem);

                //Configure it
                newItem.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                newItem.VerticalAlignment = VerticalAlignment.Top;
                newItem.Width = double.NaN;
                newItem.Height = double.NaN;
                newItem.Margin = new Thickness(0, 0, 0, 8);

                //Inform the data about the save
                newItem.SetInstanceId(instanceItem.instanceId);
                newItem.SetInstanceVersion(instanceItem.instanceVersion);
                newItem.SetInstanceTheme(instanceItem.instanceTheme);
                newItem.SetInstanceDescription(instanceItem.instanceDescription);
                newItem.SetInstanceIconUrl(instanceItem.iconUrl);
                newItem.SetInstanceBackgroundVideoUrl(instanceItem.backgroundVideoUrl);
                newItem.SetInstanceBackgroundImageUrl(instanceItem.backgroundImageUrl);
                newItem.SetInstanceLogoUrl(instanceItem.logoUrl);
                newItem.RegisterOnClickCallback((thisInstanceId, mainWindow) => { mainWindow.SelectInstanceFromInstanceCatalogSelector(thisInstanceId); });
                newItem.Prepare();

                //Wait before render next to avoid UI freezing
                yield return new WaitForSeconds(0.7f);
            }

            //Wait some time
            yield return new WaitForSeconds(0.5f);

            //Auto select the last selected instance from instances catalog
            SelectInstanceFromInstanceCatalogSelector(preferences.loadedData.lastInstanceSelected);
            //Load and render all instances icons
            LoadAndRenderAllInstancesIcons();

            //Enable the instance select button
            instanceSelector.Visibility = Visibility.Visible;

            //Auto clear routine reference
            instancesCatalogListUpdateRoutine = null;
        }

        public void SelectInstanceFromInstanceCatalogSelector(int instanceId)
        {
            //Inform the new currently selected game instance
            currentSelectedGameInstanceId = instanceId;

            //If the instance selector is opened, close it
            if (isInstanceSelectorDropdownOpen == true)
                ToggleInstanceSelectorDropdown();

            //Disable update warning
            updateWarn.Visibility = Visibility.Collapsed;
            //Disable the smartupdater update button
            smartUpdaterUpdateButton.Visibility = Visibility.Collapsed;
            smartUpdaterUpdateButton.IsEnabled = false;

            //Select the desired game instance, this will update the UI to show the metadata of the selected game instanace
            instantiatedInstanceItems[instanceId].SelectThisAsActiveGameInstance();

            //If the data of the selected instance, is not downloaded, enable the download button
            if (File.Exists((modpackPath + @"/Game/instances" + gameInstancesCatalog.loadedData.availableInstances[instanceId].instanceFolderName + @"/local-version.txt")) == false)
            {
                //Change to corrent play button
                loadingButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Visible;

                //...
            }

            //If the data of the selected instance, is downloaded, enable the play button
            if (File.Exists((modpackPath + @"/Game/instances" + gameInstancesCatalog.loadedData.availableInstances[instanceId].instanceFolderName + @"/local-version.txt")) == true)
            {
                //Change to corrent play button
                loadingButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Visible;

                //If the installed instance version, is different from the obtained from catalog, notify the user about a update
                string currentInstalledInstanceVersion = File.ReadAllText((modpackPath + @"/Game/instances" + gameInstancesCatalog.loadedData.availableInstances[instanceId].instanceFolderName + @"/local-version.txt"));
                string lastInstanceVersion = gameInstancesCatalog.loadedData.availableInstances[instanceId].currentDataVersion.ToString();
                if (currentInstalledInstanceVersion != lastInstanceVersion)
                {
                    updateWarn.Visibility = Visibility.Visible;
                    smartUpdaterUpdateButton.Visibility = Visibility.Visible;
                    smartUpdaterUpdateButton.IsEnabled = gameInstancesCatalog.loadedData.availableInstances[instanceId].isUpdatableUsingSmartUpdater;
                }
            }

            //Update the first launch notification
            UpdateTheInstanceFirstLaunchNotification(instanceId);

            //Save the new selected instance
            preferences.loadedData.lastInstanceSelected = currentSelectedGameInstanceId;
            preferences.Save();
        }

        private void InstallGameInstance(int instanceIdToInstall)
        {
            //Change to corrent play button
            loadingButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            playButton.Visibility = Visibility.Collapsed;
            loadingButton.Visibility = Visibility.Visible;

            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Inform that can't close the launcher
            canCloseWindow = false;

            //Show the status and progressbar
            statusArea.Visibility = Visibility.Visible;

            //Start a new thread to download the requested instance
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { modpackPath, instanceIdToInstall.ToString() });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the start params
                string modpackPathRoot = startParams[0];
                int requestedInstanceId = int.Parse(startParams[1]);
                //Get the data about the requested instance to install
                GameInstance instanceInfo = new InstancesCatalog((modpackPath + @"/Downloads/instances-catalog.json")).loadedData.availableInstances[requestedInstanceId];

                //Report the progress
                threadTools.ReportNewProgress("Inicializando-I-0-0");

                //Wait some time
                threadTools.MakeThreadSleep(3000);

                //Try to do the task
                try
                {
                    //Report the progress
                    threadTools.ReportNewProgress("Verificando Java-I-0-0");

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Get the required version of Java
                    string requiredJavaVersion = instanceInfo.requiresJava;
                    //Check if the requested version of Java is installed in the system
                    if (File.Exists((modpackPathRoot + @"/Java/" + requiredJavaVersion + "/local-version.txt")) == false)
                    {
                        //Report the progress
                        threadTools.ReportNewProgress("Preparando download do Java " + requiredJavaVersion + "-I-0-0");

                        //Wait some time
                        threadTools.MakeThreadSleep(3000);

                        //Download java data info
                        if (true == true)
                        {
                            //Prepare the target download URL
                            string downloadUrl = (@"https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/java-openjdk/" + requiredJavaVersion + @"/openjdk-data-info.json");
                            string saveAsPath = (modpackPathRoot + @"/Cache/openjdk-data-info.json");
                            //Download the current version file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                            httpRequestResult.EnsureSuccessStatusCode();
                            Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                            FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            downloadStream.CopyTo(fileStream);
                            httpClient.Dispose();
                            fileStream.Dispose();
                            fileStream.Close();
                            downloadStream.Dispose();
                            downloadStream.Close();
                        }

                        //Parse the download info file
                        CurrentGameDataInfo javaDataInfo = new CurrentGameDataInfo((modpackPathRoot + @"/Cache/openjdk-data-info.json"));

                        //Report the progress
                        threadTools.ReportNewProgress("Baixando Java " + requiredJavaVersion + "-D-0-100");

                        //Wait some time
                        threadTools.MakeThreadSleep(3000);

                        //Download java data
                        if (true == true)
                        {
                            //Prepare the list of downloaded files
                            List<string> downloadedFilesList = new List<string>();

                            //Download all zip parts
                            for (int i = 0; i < javaDataInfo.loadedData.downloads.Length; i++)
                            {
                                //Split download URL parts
                                string[] downloadUriParts = javaDataInfo.loadedData.downloads[i].Split("/");
                                //Prepare the save as path
                                string saveAsPath = (modpackPathRoot + @"/Cache/" + downloadUriParts[downloadUriParts.Length - 1]);
                                //Download the current file
                                HttpClient httpClient = new HttpClient();
                                HttpResponseMessage httpRequestResult = httpClient.GetAsync(javaDataInfo.loadedData.downloads[i]).Result;
                                httpRequestResult.EnsureSuccessStatusCode();
                                Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                                FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                                downloadStream.CopyTo(fileStream);
                                httpClient.Dispose();
                                fileStream.Dispose();
                                fileStream.Close();
                                downloadStream.Dispose();
                                downloadStream.Close();

                                //Add downloaded file to list
                                downloadedFilesList.Add(saveAsPath);

                                //Inform the progress
                                threadTools.ReportNewProgress(("Baixando Java " + requiredJavaVersion + "-D-" + (i + 1) + "-" + javaDataInfo.loadedData.downloads.Length));
                            }

                            //Wait some time
                            threadTools.MakeThreadSleep(3000);

                            //Report the progress
                            threadTools.ReportNewProgress("Instalando Java " + requiredJavaVersion + "-I-0-0");

                            //Wait some time
                            threadTools.MakeThreadSleep(3000);

                            //Create the folder for Java
                            if (Directory.Exists((modpackPathRoot + @"/Java/" + requiredJavaVersion)) == false)
                                Directory.CreateDirectory((modpackPathRoot + @"/Java/" + requiredJavaVersion));

                            //Extract downloaded file
                            Process process = new Process();
                            process.StartInfo.FileName = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip", "7z.exe");
                            process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip");
                            process.StartInfo.Arguments = "x \"" + downloadedFilesList[0] + "\" -o\"" + (modpackPathRoot + @"/Java/" + requiredJavaVersion) + "\" -y";
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
                            process.StartInfo.RedirectStandardOutput = true;
                            process.Start();
                            //Wait process finishes
                            process.WaitForExit();

                            //Store the new downloaded data version
                            File.WriteAllText((modpackPathRoot + @"/Java/" + requiredJavaVersion + @"/local-version.txt"), requiredJavaVersion);
                        }
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Preparando download da instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-I-0-0");

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Download instance data info
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = (instanceInfo.dataDownloadIndexUrl);
                        string saveAsPath = (modpackPathRoot + @"/Cache/instance-data-info.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //Parse the download info file
                    CurrentGameDataInfo instanceDataInfo = new CurrentGameDataInfo((modpackPathRoot + @"/Cache/instance-data-info.json"));

                    //Report the progress
                    threadTools.ReportNewProgress("Baixando instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-D-0-100");

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Download instance data
                    if (true == true)
                    {
                        //Prepare the list of downloaded files
                        List<string> downloadedFilesList = new List<string>();

                        //Download all zip parts
                        for (int i = 0; i < instanceDataInfo.loadedData.downloads.Length; i++)
                        {
                            //Split download URL parts
                            string[] downloadUriParts = instanceDataInfo.loadedData.downloads[i].Split("/");
                            //Prepare the save as path
                            string saveAsPath = (modpackPathRoot + @"/Cache/" + downloadUriParts[downloadUriParts.Length - 1]);
                            //Download the current file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(instanceDataInfo.loadedData.downloads[i]).Result;
                            httpRequestResult.EnsureSuccessStatusCode();
                            Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                            FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            downloadStream.CopyTo(fileStream);
                            httpClient.Dispose();
                            fileStream.Dispose();
                            fileStream.Close();
                            downloadStream.Dispose();
                            downloadStream.Close();

                            //Add downloaded file to list
                            downloadedFilesList.Add(saveAsPath);

                            //Inform the progress
                            threadTools.ReportNewProgress(("Baixando instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-D-" + (i + 1) + "-" + instanceDataInfo.loadedData.downloads.Length));
                        }

                        //Wait some time
                        threadTools.MakeThreadSleep(3000);

                        //Report the progress
                        threadTools.ReportNewProgress("Instalando instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-I-0-0");

                        //Wait some time
                        threadTools.MakeThreadSleep(3000);

                        //Extract downloaded file
                        Process process = new Process();
                        process.StartInfo.FileName = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip", "7z.exe");
                        process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip");
                        process.StartInfo.Arguments = "x \"" + downloadedFilesList[0] + "\" -o\"" + (modpackPathRoot + @"/Game/instances") + "\" -y";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        //Wait process finishes
                        process.WaitForExit();

                        //Store the new downloaded data version
                        File.WriteAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/local-version.txt"), instanceInfo.currentDataVersion.ToString());

                        //Store the file that signs that the instance was not runned yet
                        File.WriteAllText((modpackPathRoot + @"/Game/instances/" + requestedInstanceId + @".nfr"), "Not runned yet!");
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Finalizando instalação da instância-I-0-0");

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Install the icon of this instance in the launcher
                    if (File.Exists((modpackPathRoot + @"/Game/icons/instance_" + requestedInstanceId + @".png")) == true)
                        File.Delete((modpackPathRoot + @"/Game/icons/instance_" + requestedInstanceId + @".png"));
                    File.Copy((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance_ic.png"), (modpackPathRoot + @"/Game/icons/instance_" + requestedInstanceId + @".png"));

                    //Extract the UI from "instances.cfg" of the instances
                    string instanceGeneralConfig = File.ReadAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg")).Split("[UI]")[0];
                    string instanceUiConfig = File.ReadAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg")).Split("[UI]")[1];
                    //Overwright the file with only general config
                    File.WriteAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg"), instanceGeneralConfig);

                    //Open the "instances.cfg"
                    PrismLauncherCfgFile instanceCfg = new PrismLauncherCfgFile((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg"));

                    //Update the settings
                    instanceCfg.UpdateValue("iconKey", ("instance_" + requestedInstanceId));
                    instanceCfg.UpdateValue("OverrideJavaLocation", "true");
                    instanceCfg.UpdateValue("JavaPath", (modpackPath.Replace("/", "\\") + @"\Java\" + requiredJavaVersion + @"\bin\javaw.exe").Replace("\\", "/"));

                    //Save the "instances.cfg"
                    instanceCfg.Save();

                    //Add the UI to "instances.cfg" of the instances
                    string instancesCfgTxt = File.ReadAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg"));
                    //Overwright the file with the general+ui configs
                    File.WriteAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/instance.cfg"), (instancesCfgTxt + "\n[UI]" + instanceUiConfig));

                    //Do patch in text files informed in the instance catalog
                    for (int i = 0; i < instanceInfo.textFilesToPatchInData.Length; i++)
                    {
                        //Get this text file to be patched, info
                        TextFileToPatchInData textFileToPatch = instanceInfo.textFilesToPatchInData[i];
                        string fileTemplatePathToEdit = (modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + textFileToPatch.fileTemplatePathInsideInstance);
                        string fileToPatchPath = (modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + textFileToPatch.fileToBePatchedPathInsideInstance);

                        //If the file template not exists, skip to next
                        if (File.Exists(fileTemplatePathToEdit) == false)
                            continue;
                        //If the file to patch not exists, skip to next
                        if (File.Exists(fileToPatchPath) == false)
                            continue;

                        //Read the template file
                        string fileTemplateContent = File.ReadAllText(fileTemplatePathToEdit);

                        //Do each required patch, inside this file template to patch
                        foreach (KeyAndValueToPatchInTemplateFile requiredPatch in textFileToPatch.keysAndValuesToPatchInTemplateFile)
                        {
                            //If this type of patch is a VARIABLE
                            if (requiredPatch.type == "VARIABLE")
                            {
                                //If the variable to put is "INSTANCE_HTML_DOCUMENTATION_PATH"...
                                //   Insert the HTML path for documentation, in this patch
                                if (requiredPatch.value == "INSTANCE_HTML_DOCUMENTATION_PATH")
                                    fileTemplateContent = fileTemplateContent.Replace(
                                                          requiredPatch.key, 
                                                          (@"file:///" + modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + requiredPatch.contextOptionalStrings[0]).Replace("\\", "/").Replace(" ", "%20")
                                                          );

                                //If the variable to put is "JAVA_VERSION"...
                                //   Insert the required Java version, in this patch
                                if (requiredPatch.value == "JAVA_VERSION")
                                    fileTemplateContent = fileTemplateContent.Replace(
                                                          requiredPatch.key, 
                                                          requiredJavaVersion
                                                          );

                                //If the variable to put is "JAVA_PATH"...
                                //   Insert the required Java version, path to the "javaw.exe", in this patch
                                if (requiredPatch.value == "JAVA_PATH")
                                    fileTemplateContent = fileTemplateContent.Replace(
                                                          requiredPatch.key,
                                                          (modpackPathRoot + @"/Java/" + requiredJavaVersion + "/bin/javaw.exe").Replace("\\", "/")
                                                          );
                            }
                            //If this type of patch is a TEXT
                            if (requiredPatch.type == "TEXT")
                            {
                                //Apply this text patch
                                fileTemplateContent = fileTemplateContent.Replace(requiredPatch.key, requiredPatch.value);
                            }
                        }

                        //Write the template file, now patched, overwriting the file to be patched
                        File.WriteAllText(fileToPatchPath, fileTemplateContent);
                    }

                    //Return a success response
                    return new string[] { "success", requestedInstanceId.ToString() };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error", requestedInstanceId.ToString() };
                }

                //Finish the thread...
                return new string[] { "none", requestedInstanceId.ToString() };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //Split the new progress info
                string progressMessage = newProgress.Split("-")[0];
                bool isIndeterminate = (newProgress.Split("-")[1] == "I" ? true : false);
                int progressValue = int.Parse(newProgress.Split("-")[2]);
                int maxProgress = int.Parse(newProgress.Split("-")[3]);

                //Update the UI
                statusText.Text = progressMessage;
                if (isIndeterminate == false)
                {
                    statusProgressBar.Value = progressValue;
                    statusProgressBar.Maximum = maxProgress;
                    statusProgressBar.IsIndeterminate = false;
                }
                if (isIndeterminate == true)
                {
                    statusProgressBar.Value = 0.0f;
                    statusProgressBar.Maximum = 100.0f;
                    statusProgressBar.IsIndeterminate = true;
                }
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];
                int requestedInstanceId = int.Parse(backgroundResult[1]);

                //If have a error...
                if (threadTaskResponse != "success")
                {
                    //Show error
                    MessageBox.Show("Não foi possível instalar a instância de jogo. Por favor, tente novamente mais tarde!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);

                    //Change to corrent play button
                    loadingButton.Visibility = Visibility.Collapsed;
                    installButton.Visibility = Visibility.Collapsed;
                    playButton.Visibility = Visibility.Collapsed;
                    installButton.Visibility = Visibility.Visible;

                    //Enable the instance select button
                    instanceSelector.Visibility = Visibility.Visible;

                    //Inform that can close the launcher
                    canCloseWindow = true;

                    //Hide the status and progressbar
                    statusArea.Visibility = Visibility.Collapsed;
                }

                //If have sucess...
                if (threadTaskResponse == "success")
                {
                    //Change to corrent play button
                    loadingButton.Visibility = Visibility.Collapsed;
                    installButton.Visibility = Visibility.Collapsed;
                    playButton.Visibility = Visibility.Collapsed;
                    playButton.Visibility = Visibility.Visible;

                    //Enable the instance select button
                    instanceSelector.Visibility = Visibility.Visible;

                    //Update the first launch notification
                    UpdateTheInstanceFirstLaunchNotification(requestedInstanceId);

                    //Inform that can close the launcher
                    canCloseWindow = true;

                    //Hide the status and progressbar
                    statusArea.Visibility = Visibility.Collapsed;
                }
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void PlayGameInstance(int instanceIdToPlay)
        {
            //Start a new thread to download the requested instance
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { modpackPath, instanceIdToPlay.ToString(), playerNickname.Text });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the start params
                string modpackPathRoot = startParams[0];
                int requestedInstanceId = int.Parse(startParams[1]);
                string playerUsername = startParams[2];
                //Get the data about the requested instance to install
                GameInstance instanceInfo = new InstancesCatalog((modpackPathRoot + @"/Downloads/instances-catalog.json")).loadedData.availableInstances[requestedInstanceId];
                //Store the information about have internet or not
                bool isMachineWithInternet = true;

                //Change the UI to playing
                threadTools.ReportNewProgress("step0");

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Try to do the task
                try
                {
                    //Inform that will be downloading the settings
                    threadTools.ReportNewProgress("step1");

                    //Wait some time
                    threadTools.MakeThreadSleep(500);

                    //Try to download and apply optimized options
                    try 
                    {
                        //If the optimized options file, already exists, clear it
                        if (File.Exists((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options.txt")) == true)
                            File.Delete((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options.txt"));
                        //Download optimized options for this instance
                        if (true == true)
                        {
                            //Prepare the target download URL
                            string downloadUrl = (instanceInfo.optimizedOptionsUrl);
                            string saveAsPath = (modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options.txt");
                            //Download the current version file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                            httpRequestResult.EnsureSuccessStatusCode();
                            Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                            FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            downloadStream.CopyTo(fileStream);
                            httpClient.Dispose();
                            fileStream.Dispose();
                            fileStream.Close();
                            downloadStream.Dispose();
                            downloadStream.Close();
                        }
                        //If the optimized options to apply, file, already exists, clear it
                        if (File.Exists((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options-to-apply-in-clients.ini")) == true)
                            File.Delete((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options-to-apply-in-clients.ini"));
                        //Download optimized options to apply for this instance
                        if (true == true)
                        {
                            //Prepare the target download URL
                            string downloadUrl = (instanceInfo.optimizedOptionsToApplyInGameUrl);
                            string saveAsPath = (modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options-to-apply-in-clients.ini");
                            //Download the current version file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                            httpRequestResult.EnsureSuccessStatusCode();
                            Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                            FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            downloadStream.CopyTo(fileStream);
                            httpClient.Dispose();
                            fileStream.Dispose();
                            fileStream.Close();
                            downloadStream.Dispose();
                            downloadStream.Close();
                        }

                        //Inform that will be downloading the settings
                        threadTools.ReportNewProgress("step2");

                        //Wait some time
                        threadTools.MakeThreadSleep(500);

                        //Apply the optized options to game
                        if (true == true)
                        {
                            //Build a dictionary of desired options to apply in the user game install
                            Dictionary<string, string> optionsToApply = new Dictionary<string, string>();
                            //Fill the dictionary of desired options to apply in the user game, using the file obtained from the server
                            foreach (string line in File.ReadAllLines((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options-to-apply-in-clients.ini")))
                            {
                                //If the line contains header, ignore it
                                if (line.Contains("[   ") == true)
                                    continue;

                                //If the option name of this line not exists in dictionary, add it
                                if (optionsToApply.ContainsKey(line) == false)
                                    optionsToApply.Add(line, "");
                            }

                            //Get all values for all settings to be applied, from the downloaded options
                            string[] downloadedOptionsLines = File.ReadAllLines((modpackPathRoot + @"/Cache/instance-" + requestedInstanceId + "-options.txt"));
                            //Read each line
                            foreach (string line in downloadedOptionsLines)
                            {
                                //Split this setting by the key and value
                                string[] lineSplitted = line.Split(':');
                                string key = lineSplitted[0];
                                string value = lineSplitted[1];

                                //If this key don't exists in the dictionary, skip it
                                if (optionsToApply.ContainsKey(key) == false)
                                    continue;

                                //Save the value of downloaded options, for the current option to the dictionary
                                optionsToApply[key] = value;
                            }

                            //Apply all settings to be applied, to the local user game options
                            string[] localOptionsLines = File.ReadAllLines((modpackPathRoot + @"/Game/instances/" + instanceInfo.instanceFolderName + "/minecraft/options.txt"));
                            //Read each line and apply the settings
                            for (int i = 0; i < localOptionsLines.Length; i++)
                            {
                                //Split this setting by the key and value
                                string[] lineSplitted = localOptionsLines[i].Split(':');
                                string key = lineSplitted[0];
                                string value = lineSplitted[1];

                                //If this key don't exists in the dictionary, skip it
                                if (optionsToApply.ContainsKey(key) == false)
                                    continue;

                                //Change this setting to be the same setting of download options
                                localOptionsLines[i] = (key + ":" + optionsToApply[key]);
                            }
                            //Save the modified options file
                            File.WriteAllLines((modpackPathRoot + @"/Game/instances/" + instanceInfo.instanceFolderName + "/minecraft/options.txt"), localOptionsLines);

                            //Add all options found in downloaded options, that not exists in local game options
                            List<string> localOptionsLinesList = File.ReadAllLines((modpackPathRoot + @"/Game/instances/" + instanceInfo.instanceFolderName + "/minecraft/options.txt")).ToList();
                            //Check each key inside the options to apply
                            foreach (var item in optionsToApply)
                            {
                                //Get the key and value of this option
                                string optionKey = item.Key;
                                string optionValue = item.Value;

                                //Store if found the options in the game options
                                bool foundThisOption = false;
                                //Search the option...
                                foreach (string line in localOptionsLinesList)
                                    if (line.Contains(optionKey) == true)
                                    {
                                        foundThisOption = true;
                                        break;
                                    }

                                //If found this option, continue to check next option
                                if (foundThisOption == true)
                                    continue;

                                //If this option to apply have a empty value, ignore it
                                if (optionValue == "")
                                    continue;

                                //If not found this option in local game options, add it to the options
                                localOptionsLinesList.Add((optionKey + ":" + optionValue));
                            }
                            //Save the modified options file
                            File.WriteAllLines((modpackPathRoot + @"/Game/instances/" + instanceInfo.instanceFolderName + "/minecraft/options.txt"), localOptionsLinesList.ToArray());
                        }
                    }
                    catch(Exception ex) 
                    {
                        //Inform that don't have internet connection on machine
                        isMachineWithInternet = false;
                    }

                    //Inform that will be downloading the settings
                    threadTools.ReportNewProgress("step3");

                    //Wait some time
                    threadTools.MakeThreadSleep(500);

                    //Create a new process of the game
                    Process newProcess = new Process();
                    newProcess.StartInfo.FileName = System.IO.Path.Combine(modpackPathRoot, "Game", "prismlauncher.exe");
                    newProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPathRoot, "Game");
                    if (isMachineWithInternet == true)
                        newProcess.StartInfo.Arguments = "--launch \"" + instanceInfo.instanceFolderName.Replace("/", "") + "\"";
                    if (isMachineWithInternet == false)
                        newProcess.StartInfo.Arguments = "--launch \"" + instanceInfo.instanceFolderName.Replace("/", "") + "\" --offline \"" + playerUsername + "\"";
                    newProcess.Start();
                    //Create a monitor loop
                    while (true)
                    {
                        //Wait some time
                        threadTools.MakeThreadSleep(3000);

                        //If was finished the game process, break the monitor loop
                        if (newProcess.HasExited == true)
                            break;
                    }
                    //If the file that signs that the instance was not runned yet, exits, delete it
                    if (File.Exists((modpackPathRoot + @"/Game/instances/" + requestedInstanceId + @".nfr")) == true)
                        File.Delete((modpackPathRoot + @"/Game/instances/" + requestedInstanceId + @".nfr"));

                    //Return a success response
                    return new string[] { "success", requestedInstanceId.ToString() };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error", requestedInstanceId.ToString() };
                }

                //Finish the thread...
                return new string[] { "none", requestedInstanceId.ToString() };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //If is step 0 of play
                if (newProgress == "step0")
                {
                    //Inform that is playing game
                    isPlayingGame = true;
                    //Change to corrent play button
                    loadingButton.Visibility = Visibility.Collapsed;
                    installButton.Visibility = Visibility.Collapsed;
                    playButton.Visibility = Visibility.Collapsed;
                    loadingButton.Visibility = Visibility.Visible;
                    //Disable the instance select button
                    instanceSelector.Visibility = Visibility.Collapsed;
                    //Disable the nickname display
                    nicknameDisplay.Visibility = Visibility.Collapsed;
                    //Show the status and progressbar
                    statusArea.Visibility = Visibility.Visible;
                    statusText.Text = "Carregando";
                    statusProgressBar.Value = 0.0f;
                    statusProgressBar.Maximum = 100.0f;
                    statusProgressBar.IsIndeterminate = true;
                    //Stop the music
                    SetMusicState(MusicState.Mutted);
                    muteMusicBtn.Visibility = Visibility.Collapsed;
                    //Stop the background video
                    if (instanceBgVideo.Source != null)
                    {
                        instanceBgVideo.Stop();
                        instanceBgVideo.Close();
                    }
                    instanceBgVideo.Source = null;
                }
                //If is step 1 of play
                if (newProgress == "step1")
                {
                    //Show the status and progressbar
                    statusText.Text = "Carregando configurações otimizadas";
                }
                //If is step 2 of play
                if (newProgress == "step2")
                {
                    //Show the status and progressbar
                    statusText.Text = "Aplicando configurações otimizadas";
                }
                //If is step 3 of play
                if (newProgress == "step3")
                {
                    //Show the status and progressbar
                    string instanceTheme = gameInstancesCatalog.loadedData.availableInstances[instanceIdToPlay].instanceTheme;
                    string instanceVersion = gameInstancesCatalog.loadedData.availableInstances[instanceIdToPlay].instanceVersion;
                    statusText.Text = ("Rodando instância \"" + instanceTheme + "\" v" + instanceVersion);
                    //Minimize the Launcher
                    SetLauncherState(LauncherState.SystemTray);
                }
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];
                int requestedInstanceId = int.Parse(backgroundResult[1]);

                //If have a error...
                if (threadTaskResponse != "success")
                {
                    //Show error
                    MessageBox.Show("Houve um problema ao executar o jogo. Por favor, tente re-iniciar o Minecraft+ Launcher!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                //Restore the Launcher and UI...

                //Inform that is not playing game
                isPlayingGame = false;
                //Change to corrent play button
                loadingButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Visible;
                //Enable the instance select button
                instanceSelector.Visibility = Visibility.Visible;
                //Enable the nickname display
                nicknameDisplay.Visibility = Visibility.Visible;
                //Hide the status and progressbar
                statusArea.Visibility = Visibility.Collapsed;
                //Restore the music, if desired 
                if (preferences.loadedData.isMusicMuted == false)
                    SetMusicState(MusicState.Playing);
                muteMusicBtn.Visibility = Visibility.Visible;
                //Re-select the same instance to make video play again
                SelectInstanceFromInstanceCatalogSelector(requestedInstanceId);
                //Update the first launch notification
                UpdateTheInstanceFirstLaunchNotification(requestedInstanceId);
                //Restore the Launcher
                SetLauncherState(LauncherState.Normal);
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void OpenMoreOptionsAboutGameInstance(object sender, RoutedEventArgs routedEventArgs)
        {
            //If a context menu don't was setted up, set it up
            if (moreBtn.ContextMenu == null)
            {
                //Prepare the context menu
                moreBtn.ContextMenu = new ContextMenu();

                //Add the option for a title
                if (playContextMenuTitle == null)
                    playContextMenuTitle = new MenuItem();
                playContextMenuTitle.Header = "PlaceHolder";
                playContextMenuTitle.IsEnabled = false;
                moreBtn.ContextMenu.Items.Add(playContextMenuTitle);

                //Add the option for a space
                MenuItem itemSpace0 = new MenuItem();
                itemSpace0.Header = " ";
                itemSpace0.IsEnabled = false;
                moreBtn.ContextMenu.Items.Add(itemSpace0);

                //Add the option for open java folder
                MenuItem openJavaFolder = new MenuItem();
                openJavaFolder.Header = "Abrir pasta do Java";
                openJavaFolder.Click += (s, e) => { OpenMoreOptions_OpenJavaFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openJavaFolder);

                //Add the option for open crash reports folder
                MenuItem openCrashReportsFolder = new MenuItem();
                openCrashReportsFolder.Header = "Abrir pasta de Crash Reports";
                openCrashReportsFolder.Click += (s, e) => { OpenMoreOptions_OpenCrashReportsFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openCrashReportsFolder);

                //Add the option for open logs folder
                MenuItem openLogsFolder = new MenuItem();
                openLogsFolder.Header = "Abrir pasta de Logs";
                openLogsFolder.Click += (s, e) => { OpenMoreOptions_OpenLogsFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openLogsFolder);

                //Add the option for open mods folder
                MenuItem openModsFolder = new MenuItem();
                openModsFolder.Header = "Abrir pasta de Mods";
                openModsFolder.Click += (s, e) => { OpenMoreOptions_OpenModsFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openModsFolder);

                //Add the option for open shader packs folder
                MenuItem openShaderPacksFolder = new MenuItem();
                openShaderPacksFolder.Header = "Abrir pasta de Shader Packs";
                openShaderPacksFolder.Click += (s, e) => { OpenMoreOptions_OpenShaderPacksFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openShaderPacksFolder);

                //Add the option for open resource packs folder
                MenuItem openResPacksFolder = new MenuItem();
                openResPacksFolder.Header = "Abrir pasta de Resource Packs";
                openResPacksFolder.Click += (s, e) => { OpenMoreOptions_OpenResPacksFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openResPacksFolder);

                //Add the option for open saves folder
                MenuItem openSavesFolder = new MenuItem();
                openSavesFolder.Header = "Abrir pasta de Saves";
                openSavesFolder.Click += (s, e) => { OpenMoreOptions_OpenSavesFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openSavesFolder);

                //Add the option for open screenshots folder
                MenuItem openScreenshotsFolder = new MenuItem();
                openScreenshotsFolder.Header = "Abrir pasta de Screenshots";
                openScreenshotsFolder.Click += (s, e) => { OpenMoreOptions_OpenScreenshotsFolder(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openScreenshotsFolder);

                //Add the option for smart updater
                if (smartUpdaterUpdateButton == null)
                    smartUpdaterUpdateButton = new MenuItem();
                smartUpdaterUpdateButton.Header = "Atualizar com o SmartUpdater";
                smartUpdaterUpdateButton.Click += (s, e) => { OpenMoreOptions_OpenUpdateWithSmartUpdate(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(smartUpdaterUpdateButton);

                //Add the option for uninstall instance
                MenuItem openUninstallInstance = new MenuItem();
                openUninstallInstance.Header = "Desinstalar";
                openUninstallInstance.Click += (s, e) => { OpenMoreOptions_OpenUninstallInstance(this.currentSelectedGameInstanceId); };
                moreBtn.ContextMenu.Items.Add(openUninstallInstance);
            }

            //Show the correct title on the context menu
            string instanceTheme = gameInstancesCatalog.loadedData.availableInstances[this.currentSelectedGameInstanceId].instanceTheme;
            string instanceVersion = gameInstancesCatalog.loadedData.availableInstances[this.currentSelectedGameInstanceId].instanceVersion;
            playContextMenuTitle.Header = ("Opções para \"" + instanceTheme + " v" + instanceVersion + "\"...");

            //Display the context menu
            ContextMenu contextMenu = moreBtn.ContextMenu;
            contextMenu.PlacementTarget = moreBtn;
            contextMenu.IsOpen = true;
            routedEventArgs.Handled = true;
        }

        private void OpenMoreOptions_OpenJavaFolder(int instanceIdToInteract)
        {
            //Open the Java folder for this instance
            Process.Start("explorer.exe", (modpackPath + @"/Java/" + gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].requiresJava).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenCrashReportsFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].crashReportsFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenLogsFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].logsFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenModsFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].modsFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenShaderPacksFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].shaderPacksFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenResPacksFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].resourcePacksFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenSavesFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].savesFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenScreenshotsFolder(int instanceIdToInteract)
        {
            //Get the instance info
            string instanceFolderName = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].instanceFolderName;
            string instanceFolderToOpen = gameInstancesCatalog.loadedData.availableInstances[instanceIdToInteract].screenshotsFolderPath;

            //Open the required folder
            Process.Start("explorer.exe", (modpackPath + @"/Game/instances" + instanceFolderName + instanceFolderToOpen).Replace("/", "\\"));
        }

        private void OpenMoreOptions_OpenUpdateWithSmartUpdate(int instanceIdToInteract)
        {
            //Show confirmation dialog, before continue with the update
            if (MessageBox.Show("O SmartUpdater irá atualizar a atual instância de jogo para sua última versão disponível online. Ela deverá continuar jogável como antes, mesmo offline.\n\nO SmartUpdater também irá manter conteúdos relevantes, inalterados, conteúdos como mundos, configurações, shaders, pacotes de textura e outras coisas do tipo.\n\nUma vez iniciada a atualização, você não pode interrompe-la, isso pode causar problemas graves a instância ou aos seus dados. Gostaria de iniciar a atualização com o SmartUpdater agora?", "Atualizar com o SmartUpdater", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            //Change to corrent play button
            loadingButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            playButton.Visibility = Visibility.Collapsed;
            loadingButton.Visibility = Visibility.Visible;

            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Inform that can't close the launcher
            canCloseWindow = false;

            //Show the status and progressbar
            statusArea.Visibility = Visibility.Visible;

            //Start a new thread to update the requested instance
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { modpackPath, instanceIdToInteract.ToString() });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the start params
                string modpackPathRoot = startParams[0];
                int requestedInstanceId = int.Parse(startParams[1]);
                //Get the data about the requested instance to update
                GameInstance instanceInfo = new InstancesCatalog((modpackPathRoot + @"/Downloads/instances-catalog.json")).loadedData.availableInstances[requestedInstanceId];

                //Store the reason of error, to be informed on UI in caso of error in this update
                string errorReason = "unknown reason";

                //Report the progress
                threadTools.ReportNewProgress("Inicializando-I-0-0");

                //Wait some time
                threadTools.MakeThreadSleep(3000);

                //Try to do the task
                try
                {
                    //Create the smartupdater folder to hold the temporary update data
                    if (Directory.Exists((modpackPathRoot + @"/Cache/TmpSmartUpdater")) == true)
                        Directory.Delete((modpackPathRoot + @"/Cache/TmpSmartUpdater"), true);
                    Directory.CreateDirectory((modpackPathRoot + @"/Cache/TmpSmartUpdater"));

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Preparando download do conteúdo da instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Impossível preparar o download. Verifique sua conexão a internet.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Download instance data info
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = (instanceInfo.dataDownloadIndexUrl);
                        string saveAsPath = (modpackPathRoot + @"/Cache/TmpSmartUpdater/instance-data-info.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //Parse the download info file
                    CurrentGameDataInfo instanceDataInfo = new CurrentGameDataInfo((modpackPathRoot + @"/Cache/TmpSmartUpdater/instance-data-info.json"));

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Baixando novo conteúdo da instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-D-0-100");
                    //Inform the possible error that can occur here
                    errorReason = "Impossível fazer o download. Verifique sua conexão a internet.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Prepare the list of downloaded files of instance content
                    List<string> downloadedFilesList = new List<string>();
                    //Download instance data, that is all instance content zip parts
                    for (int i = 0; i < instanceDataInfo.loadedData.downloads.Length; i++)
                    {
                        //Split download URL parts
                        string[] downloadUriParts = instanceDataInfo.loadedData.downloads[i].Split("/");
                        //Prepare the save as path
                        string saveAsPath = (modpackPathRoot + @"/Cache/TmpSmartUpdater/" + downloadUriParts[downloadUriParts.Length - 1]);
                        //Download the current file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(instanceDataInfo.loadedData.downloads[i]).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();

                        //Add downloaded file to list
                        downloadedFilesList.Add(saveAsPath);

                        //Inform the progress
                        threadTools.ReportNewProgress(("Baixando novo conteúdo da instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-D-" + (i + 1) + "-" + instanceDataInfo.loadedData.downloads.Length));
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Expandindo conteúdo baixado-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Houve um problema de I/O ao descompactar os arquivos necessários para o processo!";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Extract downloaded file
                    Process process = new Process();
                    process.StartInfo.FileName = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip", "7z.exe");
                    process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip");
                    process.StartInfo.Arguments = "x \"" + downloadedFilesList[0] + "\" -o\"" + (modpackPathRoot + @"/Cache/TmpSmartUpdater") + "\" -y";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    //Wait process finishes
                    process.WaitForExit();

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Copiando dados relevantes-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Houve um problema de I/O ao copiar os dados relevantes! A instância pode ter sido corrompida. É recomendado que você faça backup dos seus saves e então desinstale a instância e a instale novamente.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Create the directory to hold the copied data
                    Directory.CreateDirectory((modpackPathRoot + @"/Cache/TmpSmartUpdater/copied-data"));

                    //Prepare a list of all backed up folders
                    List<BackedUpInstanceFolder> backedUpFoldersList = new List<BackedUpInstanceFolder>();
                    //Do backup of all folders in list
                    foreach (string folderPath in instanceInfo.smartUpdaterFoldersListToKeep)
                    {
                        //Create a new folder backed up
                        BackedUpInstanceFolder backedUpFolder = new BackedUpInstanceFolder();

                        //Get the original folder path
                        backedUpFolder.folderOriginalPath = (modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + folderPath).Replace("/", "\\");

                        //If the requested folder don't exists, continue to next
                        if (Directory.Exists(backedUpFolder.folderOriginalPath) == false)
                            continue;

                        //Get the new folder path
                        backedUpFolder.folderNewPath = (modpackPathRoot + @"/Cache/TmpSmartUpdater/copied-data/" + new DirectoryInfo(backedUpFolder.folderOriginalPath).Name).Replace("/", "\\");

                        //Do the backup of this directory
                        CloneDirectory(backedUpFolder.folderOriginalPath, backedUpFolder.folderNewPath);

                        //Add this backed up folder to list
                        backedUpFoldersList.Add(backedUpFolder);
                    }
                    //Prepare a list of all backed up files
                    List<BackedUpInstanceFile> backedUpFilesList = new List<BackedUpInstanceFile>();
                    //Do backup of all files in list
                    foreach (string filePath in instanceInfo.smartUpdaterFilesListToKeep)
                    {
                        //Create a new file backed up
                        BackedUpInstanceFile backedUpFile = new BackedUpInstanceFile();

                        //Get the original file path
                        backedUpFile.fileOriginalPath = (modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + filePath).Replace("/", "\\");

                        //If the requested file don't exists, continue to next
                        if (File.Exists(backedUpFile.fileOriginalPath) == false)
                            continue;

                        //Get the new file path
                        backedUpFile.fileNewPath = (modpackPathRoot + @"/Cache/TmpSmartUpdater/copied-data/" + new FileInfo(backedUpFile.fileOriginalPath).Name).Replace("/", "\\");

                        //Do the backup of this file
                        File.Copy(backedUpFile.fileOriginalPath, backedUpFile.fileNewPath);

                        //Add this backed up file to list
                        backedUpFilesList.Add(backedUpFile);
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Atualizando a instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Houve um problema de I/O ao atualizar a instância! A instância pode ter sido corrompida. É recomendado que você faça backup dos seus saves e então desinstale a instância e a instale novamente.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Delete all folders of instance
                    foreach (string folderPath in Directory.GetDirectories((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName)))
                        if (Directory.Exists(folderPath) == true)
                            Directory.Delete(folderPath, true);

                    //Move all folders of downloaded instance data and put in the installed instance
                    foreach (string folderPath in Directory.GetDirectories((modpackPathRoot + @"/Cache/TmpSmartUpdater" + instanceInfo.instanceFolderName)))
                        Directory.Move(folderPath, (modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + "/" + (new DirectoryInfo(folderPath).Name)));

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Restaurando dados relevantes-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Houve um problema de I/O ao restaurar os dados relevantes da instância! A instância pode ter sido corrompida. É recomendado que você faça backup dos seus saves e então desinstale a instância e a instale novamente.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Restore each backed up folder
                    foreach (BackedUpInstanceFolder backedUpFolder in backedUpFoldersList)
                    {
                        //If the folder already exists in the target path, delete it
                        if (Directory.Exists(backedUpFolder.folderOriginalPath) == true)
                            Directory.Delete(backedUpFolder.folderOriginalPath, true);

                        //Move the backed up folder to their original path
                        Directory.Move(backedUpFolder.folderNewPath, backedUpFolder.folderOriginalPath);
                    }
                    //Restore each becked up file
                    foreach (BackedUpInstanceFile backedUpFile in backedUpFilesList)
                    {
                        //If the file already exists in the target path, delete it
                        if (File.Exists(backedUpFile.fileOriginalPath) == true)
                            File.Delete(backedUpFile.fileOriginalPath);

                        //If the file don't have their parent folder, create it
                        if (Directory.Exists((new FileInfo(backedUpFile.fileOriginalPath).DirectoryName)) == false)
                            Directory.CreateDirectory((new FileInfo(backedUpFile.fileOriginalPath).DirectoryName));

                        //Move the backed up file to their original path
                        File.Move(backedUpFile.fileNewPath, backedUpFile.fileOriginalPath);
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Report the progress
                    threadTools.ReportNewProgress("Finalizando atualização-I-0-0");
                    //Inform the possible error that can occur here
                    errorReason = "Houve um problema de I/O ao finalizar a atualização da instância! Por favor, tente atualizar a instância, novamente.";

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Store the new updated data version
                    File.WriteAllText((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName + @"/local-version.txt"), instanceInfo.currentDataVersion.ToString());

                    //Return a success response
                    return new string[] { "success", requestedInstanceId.ToString(), "all ok" };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error", requestedInstanceId.ToString(), errorReason };
                }

                //Finish the thread...
                return new string[] { "none", requestedInstanceId.ToString(), errorReason };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //Split the new progress info
                string progressMessage = newProgress.Split("-")[0];
                bool isIndeterminate = (newProgress.Split("-")[1] == "I" ? true : false);
                int progressValue = int.Parse(newProgress.Split("-")[2]);
                int maxProgress = int.Parse(newProgress.Split("-")[3]);

                //Update the UI
                statusText.Text = progressMessage;
                if (isIndeterminate == false)
                {
                    statusProgressBar.Value = progressValue;
                    statusProgressBar.Maximum = maxProgress;
                    statusProgressBar.IsIndeterminate = false;
                }
                if (isIndeterminate == true)
                {
                    statusProgressBar.Value = 0.0f;
                    statusProgressBar.Maximum = 100.0f;
                    statusProgressBar.IsIndeterminate = true;
                }
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];
                int requestedInstanceId = int.Parse(backgroundResult[1]);
                string resultReason = backgroundResult[2];

                

                //Change to corrent play button
                loadingButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Visible;

                //Enable the instance select button
                instanceSelector.Visibility = Visibility.Visible;

                //Inform that can close the launcher
                canCloseWindow = true;

                //Hide the status and progressbar
                statusArea.Visibility = Visibility.Collapsed;

                //Re-select the instance selected
                SelectInstanceFromInstanceCatalogSelector(requestedInstanceId);

                //If have a error, show error
                if (threadTaskResponse != "success")
                    MessageBox.Show(("Houve um problema ao atualizar a instância de jogo:\n\n- " + resultReason), "Erro!", MessageBoxButton.OK, MessageBoxImage.Error);

                //If have sucess, show dialog
                if (threadTaskResponse == "success")
                    MessageBox.Show("A instância de jogo, foi atualizada com sucesso.", "Atualização concluída!", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void OpenMoreOptions_OpenUninstallInstance(int instanceIdToInteract)
        {
            //Show confirmation dialog, before continue with the update
            if (MessageBox.Show("Desinstalar a instância irá apagar todos os dados dessa instância de jogo, incluindo mundos, configurações, pacotes de textura, shaders, etc. Essa ação não pode ser desfeita e NÃO afetará outras instâncias de jogo instaladas pelo Minecraft+ Launcher.\n\nGostaria de prosseguir com a desinstalação dessa instância de jogo?", "Desinstalar instância?", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            //Change to corrent play button
            loadingButton.Visibility = Visibility.Collapsed;
            installButton.Visibility = Visibility.Collapsed;
            playButton.Visibility = Visibility.Collapsed;
            loadingButton.Visibility = Visibility.Visible;

            //Disable the instance select button
            instanceSelector.Visibility = Visibility.Collapsed;

            //Inform that can't close the launcher
            canCloseWindow = false;

            //Show the status and progressbar
            statusArea.Visibility = Visibility.Visible;

            //Start a new thread to update the requested instance
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { modpackPath, instanceIdToInteract.ToString() });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the start params
                string modpackPathRoot = startParams[0];
                int requestedInstanceId = int.Parse(startParams[1]);
                //Get the data about the requested instance to update
                GameInstance instanceInfo = new InstancesCatalog((modpackPathRoot + @"/Downloads/instances-catalog.json")).loadedData.availableInstances[requestedInstanceId];

                //Report the progress
                threadTools.ReportNewProgress("Preparando-I-0-0");

                //Wait some time
                threadTools.MakeThreadSleep(3000);

                //Try to do the task
                try
                {
                    //Report the progress
                    threadTools.ReportNewProgress("Desinstalando a instância \"" + instanceInfo.instanceTheme + " v" + instanceInfo.instanceVersion + "\"-I-0-0");

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Delete the instance icon installed on launcher
                    if (File.Exists((modpackPathRoot + @"/Game/icons/instance_" + instanceInfo.instanceId + ".png")) == true)
                        File.Delete((modpackPathRoot + @"/Game/icons/instance_" + instanceInfo.instanceId + ".png"));

                    //Delete the instance folder data
                    if (Directory.Exists((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName)) == true)
                        Directory.Delete((modpackPathRoot + @"/Game/instances" + instanceInfo.instanceFolderName), true);

                    //Wait some time
                    threadTools.MakeThreadSleep(3000);

                    //Return a success response
                    return new string[] { "success", requestedInstanceId.ToString() };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error", requestedInstanceId.ToString() };
                }

                //Finish the thread...
                return new string[] { "none", requestedInstanceId.ToString() };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //Split the new progress info
                string progressMessage = newProgress.Split("-")[0];
                bool isIndeterminate = (newProgress.Split("-")[1] == "I" ? true : false);
                int progressValue = int.Parse(newProgress.Split("-")[2]);
                int maxProgress = int.Parse(newProgress.Split("-")[3]);

                //Update the UI
                statusText.Text = progressMessage;
                if (isIndeterminate == false)
                {
                    statusProgressBar.Value = progressValue;
                    statusProgressBar.Maximum = maxProgress;
                    statusProgressBar.IsIndeterminate = false;
                }
                if (isIndeterminate == true)
                {
                    statusProgressBar.Value = 0.0f;
                    statusProgressBar.Maximum = 100.0f;
                    statusProgressBar.IsIndeterminate = true;
                }
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];
                int requestedInstanceId = int.Parse(backgroundResult[1]);



                //Change to corrent play button
                loadingButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Collapsed;
                playButton.Visibility = Visibility.Collapsed;
                installButton.Visibility = Visibility.Visible;

                //Enable the instance select button
                instanceSelector.Visibility = Visibility.Visible;

                //Inform that can close the launcher
                canCloseWindow = true;

                //Hide the status and progressbar
                statusArea.Visibility = Visibility.Collapsed;

                //Re-select the instance selected
                SelectInstanceFromInstanceCatalogSelector(requestedInstanceId);

                //If have a error, show error
                if (threadTaskResponse != "success")
                    MessageBox.Show("Houve um problema ao desinstalar a instância de jogo!", "Erro!", MessageBoxButton.OK, MessageBoxImage.Error);

                //If have sucess, show dialog
                if (threadTaskResponse == "success")
                    MessageBox.Show("A instância de jogo, foi desinstalada com sucesso.", "Desinstalação finalizada!", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void LoadAndRenderAllInstancesIcons()
        {
            //Prepare a list of all instances icons URL
            List<string> instancesIconsUrl = new List<string>();

            //Build the list of instances icons URL
            foreach (GameInstance instance in gameInstancesCatalog.loadedData.availableInstances)
                instancesIconsUrl.Add(instance.iconUrl);

            //Start a new thread to download icons that not exists, and then, load and render all icons already downloaded
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, instancesIconsUrl.ToArray());
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the list of icons URL
                string[] iconsUrl = startParams;

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Try to do the task
                try
                {
                    //Download each icon
                    foreach (string iconUrl in iconsUrl)
                    {
                        //Get the intended file name for this file
                        string[] urlParts = iconUrl.Split("/");
                        string intendedFileName = (urlParts[urlParts.Length - 3] + "-" + urlParts[urlParts.Length - 1]);

                        //If this icon is already downloaded, skip it
                        if (File.Exists((modpackPath + @"/Downloads/" + intendedFileName)) == true)
                        {
                            //Send command to update this icon on UI
                            threadTools.ReportNewProgress((urlParts[urlParts.Length - 3].Replace("instance-item-", "")));

                            //Wait some time
                            threadTools.MakeThreadSleep(1000);

                            //Skip this download
                            continue;
                        }
                            

                        //Prepare the target download URL
                        string downloadUrl = iconUrl;
                        string saveAsPath = (modpackPath + @"/Downloads/" + intendedFileName);
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();

                        //Send command to update this icon on UI
                        threadTools.ReportNewProgress((urlParts[urlParts.Length - 3].Replace("instance-item-", "")));

                        //Wait some time
                        threadTools.MakeThreadSleep(1000);
                    }

                    //Return a success response
                    return new string[] { "success" };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error" };
                }

                //Finish the thread...
                return new string[] { "none" };
            };
            asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
            {
                //Get the instance id ui element and element in catalog
                int instanceGameIdToUpdateUi = int.Parse(newProgress);
                InstanceItem instanceItemToUpdate = instantiatedInstanceItems[instanceGameIdToUpdateUi];

                //Load the instance icon
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.UriSource = new Uri((modpackPath + @"/Downloads/instance-item-" + instanceGameIdToUpdateUi + "-icon.png"));
                bitmapImage.EndInit();

                //Update the instance item in UI
                instanceItemToUpdate.instanceIc.Source = bitmapImage;

                //If this instance is currently selected, show it too
                if (currentSelectedGameInstanceId == instanceGameIdToUpdateUi)
                    instanceIcImg.Source = instanceItemToUpdate.instanceIc.Source;
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) => { };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void ToggleInstanceSelectorDropdown()
        {
            //Stop all dropdown animations
            if (openInstanceSelectorDropdownRoutine != null)
            {
                openInstanceSelectorDropdownRoutine.Dispose();
                openInstanceSelectorDropdownRoutine = null;
            }
            animStoryboards["instanceSelectorDropdownOpen"].Stop();
            if (closeInstanceSelectorDropdownRoutine != null)
            {
                closeInstanceSelectorDropdownRoutine.Dispose();
                closeInstanceSelectorDropdownRoutine = null;
            }
            animStoryboards["instanceSelectorDropwdownClose"].Stop();

            //If the dropdown is open...
            if (isInstanceSelectorDropdownOpen == true)
            {
                //Change the indicator arrow
                instanceArrowDown.Visibility = Visibility.Collapsed;
                instanceArrowUp.Visibility = Visibility.Visible;

                //Run the animation of dropdown close
                openInstanceSelectorDropdownRoutine = Coroutine.Start(CloseInstanceSelectorDropdownRoutine());

                //Inform that is closed
                isInstanceSelectorDropdownOpen = false;
                //Stop here
                return;
            }

            //If the dropdown is closed...
            if (isInstanceSelectorDropdownOpen == false)
            {
                //Change the indicator arrow
                instanceArrowDown.Visibility = Visibility.Visible;
                instanceArrowUp.Visibility = Visibility.Collapsed;

                //Run the animation of dropdown open
                openInstanceSelectorDropdownRoutine = Coroutine.Start(OpenInstanceSelectorDropdownRoutine());

                //Inform that is opened
                isInstanceSelectorDropdownOpen = true;
                //Stop here
                return;
            }
        }

        private IEnumerator OpenInstanceSelectorDropdownRoutine()
        {
            //Enable the dropdown
            instanceSelectorDropDown.Visibility = Visibility.Visible;

            //Run the animation of entry
            animStoryboards["instanceSelectorDropdownOpen"].Begin();

            //Wait time of the end
            yield return new WaitForSeconds(0.35f);

            //Auto clear routine reference
            openInstanceSelectorDropdownRoutine = null;
        }

        private IEnumerator CloseInstanceSelectorDropdownRoutine()
        {
            //Run the animation of exit
            animStoryboards["instanceSelectorDropwdownClose"].Begin();

            //Wait time of the end
            yield return new WaitForSeconds(0.35f);

            //Enable the dropdown
            instanceSelectorDropDown.Visibility = Visibility.Collapsed;

            //Auto clear routine reference
            closeInstanceSelectorDropdownRoutine = null;
        }

        //Auxiliar methods

        private string GetLauncherVersion()
        {
            //Prepare te storage
            string version = "";

            //Get the version
            string[] versionNumbers = Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.');
            version = (versionNumbers[0] + "." + versionNumbers[1] + "." + versionNumbers[2]);

            //Return the version
            return version;
        }

        private void SetLauncherState(LauncherState desiredState)
        {
            //If don't have a system tray icon, create it
            if (launcherTrayIcon == null)
            {
                launcherTrayIcon = new System.Windows.Forms.NotifyIcon();
                launcherTrayIcon.Visible = true;
                launcherTrayIcon.Text = "Minecraft+ Launcher";
                launcherTrayIcon.Icon = new System.Drawing.Icon(@"Resources/system-tray.ico");
                launcherTrayIcon.MouseClick += (s, e) => { SetLauncherState(LauncherState.Normal); };
            }

            //If is desired normal state..
            if (desiredState == LauncherState.Normal)
            {
                //Enable this window
                this.Visibility = Visibility.Visible;

                //Disable the system tray
                launcherTrayIcon.Visible = false;
            }

            //If is desired system tray state...
            if (desiredState == LauncherState.SystemTray)
            {
                //Hide this window
                this.Visibility = Visibility.Collapsed;

                //Enable the system tray
                launcherTrayIcon.Visible = true;
            }
        }

        private void CloseLauncherWithError(string errorMessage)
        {
            //Show error
            MessageBox.Show(errorMessage, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            //Inform that can close
            canCloseWindow = true;
            isPlayingGame = false;
            //Close the application
            System.Windows.Application.Current.Shutdown();
        }
    
        private void SetMusicState(MusicState musicState)
        {
            //If is desired to play
            if (musicState == MusicState.Playing)
            {
                //Update the UI
                muteMusicIc.Source = new BitmapImage(new Uri(@"pack://application:,,,/Resources/music-playing.png"));
                //Start the coroutine of music play
                if (musicPlayRoutine == null)
                    musicPlayRoutine = Coroutine.Start(StartMusicPlaying());
            }

            //If is desired to mute
            if (musicState == MusicState.Mutted)
            {
                //Update the UI
                muteMusicIc.Source = new BitmapImage(new Uri(@"pack://application:,,,/Resources/music-mutted.png"));
                //Mute the music
                if (musicMediaPlayer != null && isMusicPlaying == true)
                    musicMediaPlayer.Stop();
                //Stop the music play coroutine
                if (musicPlayRoutine != null)
                    musicPlayRoutine.Dispose();
                musicPlayRoutine = null;
            }
        }

        private IEnumerator StartMusicPlaying()
        {
            //Build the musics list
            List<string> listOfMusicsPaths = new List<string>();
            listOfMusicsPaths.Add((@"Resources/launcher-music0.mp3"));
            listOfMusicsPaths.Add((@"Resources/launcher-music1.mp3"));

            //Prepare the interval of time of music
            WaitForSeconds intervalTime = new WaitForSeconds(5.0f);

            //Start the music loop
            while (true == true)
            {
                //Wait a time
                yield return new WaitForSeconds(1.0f);

                //Choose a random music
                int musicIndex = (new Random().Next(0, listOfMusicsPaths.Count));
                //Fix the index
                if (musicIndex < 0)
                    musicIndex = 0;
                if (musicIndex > (listOfMusicsPaths.Count - 1))
                    musicIndex = (listOfMusicsPaths.Count - 1);

                //If not exists a media player, create it
                if (musicMediaPlayer == null)
                    musicMediaPlayer = new MediaPlayer();

                //Load the music
                musicMediaPlayer.Open(new Uri(listOfMusicsPaths[musicIndex], UriKind.Relative));

                //Play the music
                musicMediaPlayer.MediaEnded += ((s, e) => { isMusicPlaying = false; });
                musicMediaPlayer.Volume = 1.0f;
                musicMediaPlayer.Play();
                isMusicPlaying = true;

                //Wait until music stops
                while (isMusicPlaying == true)
                    yield return intervalTime;
            }
        }
    
        private void LoadNicknameAndSkin()
        {
            //Update the UI to loading state
            playerSkin.Source = new BitmapImage(new Uri(@"pack://application:,,,/Resources/loading-skin.png"));
            playerSkinLayer.Source = null;
            playerNickname.Text = "Carregando";
            editNickBtn.Visibility = Visibility.Collapsed;

            //Load the accounts data of launcher
            AccountsData accountsData = new AccountsData((modpackPath + @"/Game/accounts.json"));

            //Display the account nickname
            playerNickname.Text = accountsData.loadedData.accounts[0].profile.name;

            //Start a thread to load the skin of player, from mojang server
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { playerNickname.Text });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Try to do the task
                try
                {
                    //Get the player nickname to load
                    string playerNickname = startParams[0];

                    //If file is already created, delete it
                    if (File.Exists((modpackPath + @"/Cache/mojang_user.json")) == true)
                        File.Delete((modpackPath + @"/Cache/mojang_user.json"));

                    //Acquire more informations about the user
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = (@"https://api.mojang.com/users/profiles/minecraft/" + playerNickname);
                        string saveAsPath = (modpackPath + @"/Cache/mojang_user.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //If the text contains error message, stop here
                    if (File.ReadAllText((modpackPath + @"/Cache/mojang_user.json")).ToLower().Contains("\"errormessage\"") == true)
                        return new string[] { "error" };

                    //Parse the mojang user information
                    MojangUserInfo mojangUserInfo = new MojangUserInfo((modpackPath + @"/Cache/mojang_user.json"));
                    string userUuid = mojangUserInfo.loadedData.id;
                    string userName = mojangUserInfo.loadedData.name;

                    //Wait some time
                    threadTools.MakeThreadSleep(500);

                    //If file is already created, delete it
                    if (File.Exists((modpackPath + @"/Cache/mojang_profile.json")) == true)
                        File.Delete((modpackPath + @"/Cache/mojang_profile.json"));

                    //Acquire informations about the user profile
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = (@"https://sessionserver.mojang.com/session/minecraft/profile/" + userUuid);
                        string saveAsPath = (modpackPath + @"/Cache/mojang_profile.json");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();
                    }

                    //Parse the mojang profile information
                    MojangProfileInfo mojangProfileInfo = new MojangProfileInfo((modpackPath + @"/Cache/mojang_profile.json"));
                    string textureDataDecoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(mojangProfileInfo.loadedData.properties[0].value));

                    //Store the texture data decoded
                    File.WriteAllText((modpackPath + @"/Cache/mojang_textures.json"), textureDataDecoded);

                    //If the decoded texture data  don't contain a skin info, cancel here
                    if (textureDataDecoded.ToLower().Contains("\"skin\"") == false)
                        return new string[] { "error" };

                    //Get the URL for the skin texture
                    string skinProcessing0 = textureDataDecoded.ToLower().Split("\"skin\"")[1].Replace("\r\n", "").Replace("\n", "").Replace("\r", "").Replace(" ", "");
                    string skinProcessing1 = skinProcessing0.Replace(":{\"url\":\"", "");
                    string skinProcessing2 = skinProcessing1.Split("\"")[0];
                    string skinTextureUrl = skinProcessing2;

                    //Wait some time
                    threadTools.MakeThreadSleep(500);

                    //Prepare the name of the skin downloaded
                    string downloadedSkinFileName = "";
                    //Download the skin texture of the player
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = skinTextureUrl;
                        string saveAsPath = (modpackPath + @"/Cache/mojang_skin" + DateTime.Now.Ticks + ".png");
                        //Download the current version file
                        HttpClient httpClient = new HttpClient();
                        HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                        httpRequestResult.EnsureSuccessStatusCode();
                        Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                        FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        downloadStream.CopyTo(fileStream);
                        httpClient.Dispose();
                        fileStream.Dispose();
                        fileStream.Close();
                        downloadStream.Dispose();
                        downloadStream.Close();

                        //Store the downloaded skin file name
                        downloadedSkinFileName = saveAsPath;
                    }

                    //Return a success response
                    return new string[] { "success", downloadedSkinFileName };
                }
                catch (Exception ex)
                {
                    //Return a error response
                    return new string[] { "error" };
                }

                //Finish the thread...
                return new string[] { "none" };
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have a error, stop the launcher
                if (threadTaskResponse != "success")
                {
                    //Show default skin
                    playerSkin.Source = new BitmapImage(new Uri(@"pack://application:,,,/Resources/default-player-skin.png"));
                    playerSkinLayer.Source = null;
                    editNickBtn.Visibility = Visibility.Visible;
                }

                //If have sucess, continue to next step
                if (threadTaskResponse == "success")
                {
                    //Load the downloaded skin
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.UriSource = new Uri(backgroundResult[1]);
                    bitmapImage.EndInit();

                    //Get resolution of the skin downloaded
                    int skinWidth = (int)bitmapImage.Width;
                    int skinHeight = (int)bitmapImage.Height;
                    
                    //If the player skin is 64x64, render the skin
                    if (skinWidth == 64 && skinHeight == 64)
                    {
                        //Extract the head of the image
                        CroppedBitmap croppedBitmapOfHead = new CroppedBitmap(bitmapImage, new Int32Rect(8, 8, 8, 8));
                        CroppedBitmap croppedBitmapOfHeadLayer = new CroppedBitmap(bitmapImage, new Int32Rect(39, 7, 10, 9));

                        //Render head of the skin bitmap
                        playerSkin.Source = croppedBitmapOfHead;

                        //Render head layer of the skin bitmap
                        playerSkinLayer.Source = croppedBitmapOfHeadLayer;
                    }
                    //If the player skin is 64x32, render the skin
                    if (skinWidth == 64 && skinHeight == 32)
                    {
                        //Extract the head of the image
                        CroppedBitmap croppedBitmapOfHead = new CroppedBitmap(bitmapImage, new Int32Rect(8, 8, 8, 8));
                        CroppedBitmap croppedBitmapOfHeadLayer = new CroppedBitmap(bitmapImage, new Int32Rect(39, 7, 10, 9));

                        //Render head of the skin bitmap
                        playerSkin.Source = croppedBitmapOfHead;

                        //Render head layer of the skin bitmap
                        playerSkinLayer.Source = croppedBitmapOfHeadLayer;
                    }

                    //Change the render mod of the image, to not use image filtering, like minecraft
                    RenderOptions.SetBitmapScalingMode(playerSkin, BitmapScalingMode.NearestNeighbor);
                    RenderOptions.SetBitmapScalingMode(playerSkinLayer, BitmapScalingMode.NearestNeighbor);

                    //Show the edit button again
                    editNickBtn.Visibility = Visibility.Visible;
                }
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }
    
        private void OpenNicknameEditor()
        {
            //Enable the interaction blocker
            interactionBlocker.Visibility = Visibility.Visible;

            //Open the window of Nickname change
            WindowNickChange nickChange = new WindowNickChange(modpackPath);
            nickChange.Closed += (s, e) =>
            {
                interactionBlocker.Visibility = Visibility.Collapsed;
                LoadNicknameAndSkin();
            };
            nickChange.Owner = this;
            nickChange.Show();
        }
    
        private void UpdateTheInstanceFirstLaunchNotification(int currentSelectedInstance)
        {
            //Check if the file that signs that the desired instance was not first runned yet, exists
            bool nfrFileExists = File.Exists((modpackPath + @"/Game/instances/" + currentSelectedInstance + @".nfr"));

            //Disable the not runned yet, warning
            firstRunWarn.Visibility = Visibility.Collapsed;

            //If the file exists, show the warning
            if (nfrFileExists == true)
                firstRunWarn.Visibility = Visibility.Visible;

            //If the file not exists, hide the warning
            if (nfrFileExists == false)
                firstRunWarn.Visibility = Visibility.Collapsed;
        }

        private void CloneDirectory(string root, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (var directory in Directory.GetDirectories(root))
            {
                //Get the path of the new directory
                var newDirectory = System.IO.Path.Combine(dest, System.IO.Path.GetFileName(directory));
                //Create the directory if it doesn't already exist
                Directory.CreateDirectory(newDirectory);
                //Recursively clone the directory
                CloneDirectory(directory, newDirectory);
            }

            foreach (var file in Directory.GetFiles(root))
            {
                File.Copy(file, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(file)));
            }
        }
    }
}