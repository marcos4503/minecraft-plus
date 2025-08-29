using Ionic.Zip;
using MarcosTomaz.ATS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Minecraft_Plus_Updater
{
    /*
     * This is the script responsible by the updater window of the updater
    */

    public partial class WindowUpdater : Window
    {
        //Cache variables
        private bool canCloseWindow = false;

        //Private variables
        private string modpackPath = "";
        private string currentVersionOfServer = "";
        private string currentVersionOfLocal = "";

        //Core methods

        public WindowUpdater()
        {
            //Check if have another process of the launcher already opened. If have, cancel this...
            string processName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 1)
            {
                //Warn about the problem
                MessageBox.Show("O Minecraft+ já está em execução!", "Erro");

                //Stop the execution of this instance
                System.Windows.Application.Current.Shutdown();

                //Cancel the execution
                return;
            }

            //Check if the launcher is already currently opened. If is, cancel this...
            Process[] processes2 = Process.GetProcessesByName("Minecraft Plus");
            if (processes2.Length > 0)
            {
                //Warn about the problem
                MessageBox.Show("O Minecraft+ já está em execução!", "Erro");

                //Stop the execution of this instance
                System.Windows.Application.Current.Shutdown();

                //Cancel the execution
                return;
            }

            //Initialize the Window
            InitializeComponent();

            //Get the modpack path
            modpackPath = (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft-plus");

            //Prepare the UI
            PrepareTheUI();
        }

        private void PrepareTheUI()
        {
            //Block the window close
            this.Closing += (s, e) =>
            {
                if (canCloseWindow == false)
                {
                    MessageBox.Show("Por favor, aguarde o processo de atualização finalizar antes de fechar o Minecraft+.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    return;
                }
            };

            //Show the starting text
            statusTxt.Text = "Inicializando...";
            progressBar.Value = 0.0f;
            progressBar.Maximum = 100.0f;
            progressBar.IsIndeterminate = true;

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
            if (Directory.Exists((modpackPath + "/Launcher")) == false)
                Directory.CreateDirectory((modpackPath + "/Launcher"));
            if (Directory.Exists((modpackPath + "/Game")) == false)
                Directory.CreateDirectory((modpackPath + "/Game"));
            if (Directory.Exists((modpackPath + "/Java")) == false)
                Directory.CreateDirectory((modpackPath + "/Java"));

            //Load the last version info
            LoadLauncherLastVersionInfo();
        }

        private void LoadLauncherLastVersionInfo()
        {
            //Start a thread to download the current version info
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Wait some time
                threadTools.MakeThreadSleep(2500);

                //Show new status
                threadTools.ReportNewProgress("Verificando atualizações...");

                //Wait some time
                threadTools.MakeThreadSleep(2500);

                //Try to do the task
                try
                {
                    //Prepare the target download URL
                    string downloadUrl = @"https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/current-launcher-version.txt";
                    string saveAsPath = (modpackPath + @"/Cache/launcher-current-version.txt");
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
                //Set the new status
                statusTxt.Text = newProgress;
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have a response not success...
                if (threadTaskResponse != "success")
                {
                    //Show error
                    MessageBox.Show("Não foi possível verificar as informações de versão. Por favor, tente novamente mais tarde.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    //Inform that can close
                    canCloseWindow = true;
                    //Open the Launcher
                    OpenLauncher();
                }

                //If have a response of success...
                if (threadTaskResponse == "success")
                {
                    //Process the version info
                    ProcessLauncherVersionInfo();
                }
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void ProcessLauncherVersionInfo()
        {
            //Store the version of server
            currentVersionOfServer = File.ReadAllText((modpackPath + "/Cache/launcher-current-version.txt"));

            //Get the local version
            if (File.Exists((modpackPath + "/Launcher/local-current-version.txt")) == true)
                currentVersionOfLocal = File.ReadAllText((modpackPath + "/Launcher/local-current-version.txt"));

            //If the version is different, go to install the update
            if (currentVersionOfServer != currentVersionOfLocal)
                DownloadUpdate();

            //If the version is equal, go to open the launcher
            if (currentVersionOfServer == currentVersionOfLocal)
                OpenLauncher();
        }

        private void DownloadUpdate()
        {
            //Show the starting update text
            statusTxt.Text = "Baixando atualização...";
            progressBar.Value = 0.0f;
            progressBar.Maximum = 100.0f;
            progressBar.IsIndeterminate = false;

            //Start a thread to start the download
            Thread thread = new Thread(() => {
                //Wait time
                Thread.Sleep(3000);

                //Start the webclient to download
                WebClient client = new WebClient();
                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler((s, e) =>
                {
                    //Run on UI Thread
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        //Show the progress
                        progressBar.Value = ((double)((double)(double.Parse(e.BytesReceived.ToString())) / (float)(double.Parse(e.TotalBytesToReceive.ToString()))) * 100.0d);
                    }));
                });
                client.DownloadFileCompleted += new AsyncCompletedEventHandler((s, e) =>
                {
                    //Run on UI Thread
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        //Go to install update
                        InstallUpdate();
                    }));
                });
                client.DownloadFileAsync(new Uri("https://tomaz-collaborations.github.io/minecraft-plus-pages/Repository-Pages/current-launcher-compilation.zip"),
                                         (modpackPath + "/Cache/current-launcher-compilation.zip"));
            });
            thread.Start();
        }

        private void InstallUpdate()
        {
            //Start a thread to install the update
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(this, new string[] { });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Wait some time
                threadTools.MakeThreadSleep(2500);

                //Change status
                threadTools.ReportNewProgress("Instalando atualização...");

                //Wait some time
                threadTools.MakeThreadSleep(2500);

                //Try to do the task
                try
                {
                    //Move the downloaded file to output directory
                    File.Move((modpackPath + "/Cache/current-launcher-compilation.zip"), (modpackPath + "/Launcher/current-launcher-compilation.zip"));

                    //Extract the file
                    ZipFile zipFile = ZipFile.Read((modpackPath + "/Launcher/current-launcher-compilation.zip"));
                    foreach (ZipEntry entry in zipFile)
                        entry.Extract((modpackPath + "/Launcher"), ExtractExistingFileAction.OverwriteSilently);
                    zipFile.Dispose();

                    //Delete the downloaded file
                    File.Delete((modpackPath + "/Launcher/current-launcher-compilation.zip"));

                    //Put the version file
                    File.WriteAllText((modpackPath + "/Launcher/local-current-version.txt"), currentVersionOfServer);

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
                //Update the UI
                statusTxt.Text = newProgress;
                progressBar.Value = 0.0f;
                progressBar.Maximum = 100.0f;
                progressBar.IsIndeterminate = true;
            };
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have a response not success, close
                if (threadTaskResponse != "success")
                {
                    //Show error
                    MessageBox.Show("Houve um erro durante o processo de atualização. Por favor, tente novamente mais tarde.",
                                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    //Inform that can close
                    canCloseWindow = true;
                    //Close the application
                    System.Windows.Application.Current.Shutdown();
                    //Cancel
                    return;
                }

                //Open the launcher
                OpenLauncher();
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void OpenLauncher()
        {
            //Register the path for the current executable
            File.WriteAllText((modpackPath + "/updater-path.mcp"), (System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));

            //If the Launcher not exists, cancel
            if (File.Exists((modpackPath + "/Launcher/Minecraft Plus.exe")) == false)
            {
                //Show a warning
                MessageBox.Show("O Minecraft+ não foi encontrado. Por favor, tente executar o Launcher novamente.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                //Close this updater
                canCloseWindow = true;
                //Close the application
                System.Windows.Application.Current.Shutdown();
                //Cancel here
                return;
            }

            //Open the launcher
            Process newProcess = new Process();
            newProcess.StartInfo.FileName = System.IO.Path.Combine(modpackPath, "Launcher", "Minecraft Plus.exe");
            newProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPath, "Launcher");
            newProcess.Start();

            //Close this updater
            canCloseWindow = true;
            //Close the application
            System.Windows.Application.Current.Shutdown();
        }
    }
}
