using MarcosTomaz.ATS;
using Minecraft_Plus.Scripts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UserControl = System.Windows.Controls.UserControl;

namespace Minecraft_Plus.Controls.ListItems
{
    /*
     * This script is responsible by the work of the "InstanceItem" that represents each game instance
     * available in the instances catalog of Launcher
    */

    public partial class InstanceItem : UserControl
    {
        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnClick(int thisInstanceId, MainWindow mainWindow);
        }

        //Cache variables
        private bool alreadyLoadingLogo = false;
        private bool alreadyLoadingBackground = false;
        private bool alreadyLoadingVideo = false;

        //Private variables
        private event ClassDelegates.OnClick onClick;
        public int gameInstanceId = -1;
        public string instanceVersion = "";
        public string instanceTheme = "";
        public string instanceDescription = "";
        public string iconUrl = "";
        public string backgroundVideoUrl = "";
        public string backgroundImageUrl = "";
        public string logoUrl = "";

        //Public variables
        public MainWindow instantiatedByWindow = null;
        public string modpackPath = "";

        //Core methods

        public InstanceItem(MainWindow instantiatedBy, string modpackPath)
        {
            //Initialize the component
            InitializeComponent();

            //Inform that is the DataConext of this User Control
            this.DataContext = this;

            //Store reference for window that was instantiated this item
            this.instantiatedByWindow = instantiatedBy;
            this.modpackPath = modpackPath;
        }

        //Public methods

        public void SetInstanceId(int instanceId)
        {
            //Set the instance id
            this.gameInstanceId = instanceId;
        }

        public void SetInstanceVersion(string instanceVersion)
        {
            //Set the instance version
            this.instanceVersion = instanceVersion;
        }

        public void SetInstanceTheme(string instanceTheme)
        {
            //Set the instance theme
            this.instanceTheme = instanceTheme;
        }

        public void SetInstanceDescription(string instanceDescription)
        {
            //Set the instance description
            this.instanceDescription = instanceDescription.Replace(" \n ", "\n");
        }

        public void SetInstanceIconUrl(string iconUrl)
        {
            //Set the instance icon url
            this.iconUrl = iconUrl;
        }

        public void SetInstanceBackgroundVideoUrl(string backgroundVideoUrl)
        {
            //Set the instance background video url
            this.backgroundVideoUrl = backgroundVideoUrl;
        }

        public void SetInstanceBackgroundImageUrl(string backgroundImageUrl)
        {
            //Set the instance background image url
            this.backgroundImageUrl = backgroundImageUrl;
        }

        public void SetInstanceLogoUrl(string logoUrl)
        {
            //Set the instance logo url
            this.logoUrl = logoUrl;
        }

        public void RegisterOnClickCallback(ClassDelegates.OnClick onClick)
        {
            //Register the event
            this.onClick = onClick;
        }

        public void Prepare()
        {
            //Prepare the color highlight
            instanceBg.MouseEnter += (s, e) => { instanceBg.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 251, 251, 251)); };
            instanceBg.MouseLeave += (s, e) => { instanceBg.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0)); };

            //Prepare the selection callback
            instanceBg.MouseUp += (s, e) => 
            {
                //Run callback
                if (this.onClick != null)
                    this.onClick(gameInstanceId, instantiatedByWindow);
            };

            //Show the information about the instance
            instanceVersionTxt.Text = instanceVersion;
            instanceThemeTxt.Text = instanceTheme;
            instanceBg.ToolTip = instanceDescription;
        }

        //Auxiliar methods

        public void SelectThisAsActiveGameInstance()
        {
            //If the currently selected istance is not this, cancel here
            if (instantiatedByWindow.currentSelectedGameInstanceId != gameInstanceId)
                return;

            //Show this selected instance information
            instantiatedByWindow.instanceVersion.Text = instanceVersion;
            instantiatedByWindow.instanceTheme.Text = instanceTheme;
            instantiatedByWindow.instanceSelector.ToolTip = instanceDescription.Replace(" \n ", "\n");
            instantiatedByWindow.instanceIcImg.Source = instanceIc.Source;

            //Load and render the other metadata of this instance
            LoadAndRenderLogo();
            LoadAndRenderBackground();
            LoadAndRenderVideo();
        }

        private void LoadAndRenderLogo()
        {
            //If is already loading the logo, cancel here
            if (alreadyLoadingLogo == true)
                return;

            //Inform that is loading the logo
            alreadyLoadingLogo = true;

            //Show temporary logo
            instantiatedByWindow.instanceLogo.Source = new BitmapImage(new Uri(@"pack://application:,,,/Resources/modpack-logo-load.png"));

            //Start a new thread to download logo, if not exists, and then, load and render the logo already downloaded
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(instantiatedByWindow, new string[] { modpackPath, logoUrl });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the logo url
                string modpackPathRoot = startParams[0];
                string logoUrlToDownload = startParams[1];

                //Try to do the task
                try
                {
                    //Get the intended file name for this file
                    string[] urlParts = logoUrlToDownload.Split("/");
                    string intendedFileName = (urlParts[urlParts.Length - 3] + "-" + urlParts[urlParts.Length - 1]);

                    //If this icon is already downloaded, skip the download
                    if (File.Exists((modpackPathRoot + @"/Downloads/" + intendedFileName)) == true)
                    {
                        //Return a success response
                        return new string[] { "success" };
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(1000);

                    //Prepare the target download URL
                    string downloadUrl = logoUrlToDownload;
                    string saveAsPath = (modpackPathRoot + @"/Downloads/" + intendedFileName);
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
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) => 
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have sucess, and the currently selected instance is this instance...
                if (threadTaskResponse == "success")
                    if (instantiatedByWindow.currentSelectedGameInstanceId == gameInstanceId)
                    {
                        //Load the instance logo
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.UriSource = new Uri((modpackPath + @"/Downloads/instance-item-" + gameInstanceId + "-logo.png"));
                        bitmapImage.EndInit();

                        //Render this logo on the screen
                        instantiatedByWindow.instanceLogo.Source = bitmapImage;
                    }

                //Inform that is not loading the logo anymore
                alreadyLoadingLogo = false;
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }
    
        private void LoadAndRenderBackground()
        {
            //If is already loading the background, cancel here
            if (alreadyLoadingBackground == true)
                return;

            //Inform that is loading the background
            alreadyLoadingBackground = true;

            //Show temporary background
            instantiatedByWindow.instanceBgImg.Source = null;

            //Start a new thread to download background, if not exists, and then, load and render the background already downloaded
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(instantiatedByWindow, new string[] { modpackPath, backgroundImageUrl });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the background url
                string modpackPathRoot = startParams[0];
                string backgroundUrlToDownload = startParams[1];

                //Try to do the task
                try
                {
                    //Get the intended file name for this file
                    string[] urlParts = backgroundUrlToDownload.Split("/");
                    string intendedFileName = (urlParts[urlParts.Length - 3] + "-" + urlParts[urlParts.Length - 1]);

                    //If this icon is already downloaded, skip the download
                    if (File.Exists((modpackPathRoot + @"/Downloads/" + intendedFileName)) == true)
                    {
                        //Return a success response
                        return new string[] { "success" };
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(1000);

                    //Prepare the target download URL
                    string downloadUrl = backgroundUrlToDownload;
                    string saveAsPath = (modpackPathRoot + @"/Downloads/" + intendedFileName);
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
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have sucess, and the currently selected instance is this instance...
                if (threadTaskResponse == "success")
                    if (instantiatedByWindow.currentSelectedGameInstanceId == gameInstanceId)
                    {
                        //Load the instance background
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.UriSource = new Uri((modpackPath + @"/Downloads/instance-item-" + gameInstanceId + "-background.png"));
                        bitmapImage.EndInit();

                        //Render this background on the screen
                        instantiatedByWindow.instanceBgImg.Source = bitmapImage;
                    }

                //Inform that is not loading the background anymore
                alreadyLoadingBackground = false;
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }

        private void LoadAndRenderVideo()
        {
            //If is already loading the video, cancel here
            if (alreadyLoadingVideo == true)
                return;

            //Inform that is loading the video
            alreadyLoadingVideo = true;

            //Reset the video player
            if (instantiatedByWindow.instanceBgVideo.Source != null)
            {
                instantiatedByWindow.instanceBgVideo.Stop();
                instantiatedByWindow.instanceBgVideo.Close();
            }
            instantiatedByWindow.instanceBgVideo.Source = null;

            //Start a new thread to download video, if not exists, and then, load and render the video already downloaded
            AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(instantiatedByWindow, new string[] { modpackPath, backgroundVideoUrl });
            asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
            asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
            {
                //Get the video url
                string modpackPathRoot = startParams[0];
                string videoUrlToDownload = startParams[1];

                //Wait some time
                threadTools.MakeThreadSleep(500);

                //Try to do the task
                try
                {
                    //Get the intended file name for this file
                    string[] urlParts = videoUrlToDownload.Split("/");
                    string intendedFileName = (urlParts[urlParts.Length - 3] + "-video.mp4");

                    //If this video is already downloaded, skip the download
                    if (File.Exists((modpackPathRoot + @"/Downloads/" + intendedFileName)) == true)
                    {
                        //Return a success response
                        return new string[] { "success" };
                    }

                    //Wait some time
                    threadTools.MakeThreadSleep(1000);

                    //Create a temporary file to store this operation files
                    if (Directory.Exists((modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3])) == true)
                        Directory.Delete((modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3]), true);
                    Directory.CreateDirectory((modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3]));

                    //Download the video info
                    if (true == true)
                    {
                        //Prepare the target download URL
                        string downloadUrl = videoUrlToDownload;
                        string saveAsPath = (modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3] + @"/" + urlParts[urlParts.Length - 3] + "-" + urlParts[urlParts.Length - 1]);
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
                    CurrentGameDataInfo videoInfo = new CurrentGameDataInfo((modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3] + @"/" + urlParts[urlParts.Length - 3] + "-" + urlParts[urlParts.Length - 1]));

                    //Download video data
                    if (true == true)
                    {
                        //Prepare the list of downloaded files
                        List<string> downloadedFilesList = new List<string>();

                        //Download all zip parts
                        for (int i = 0; i < videoInfo.loadedData.downloads.Length; i++)
                        {
                            //Split download URL parts
                            string[] downloadUriParts = videoInfo.loadedData.downloads[i].Split("/");
                            //Prepare the save as path
                            string saveAsPath = (modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3] + @"/" + downloadUriParts[downloadUriParts.Length - 1]);
                            //Download the current file
                            HttpClient httpClient = new HttpClient();
                            HttpResponseMessage httpRequestResult = httpClient.GetAsync(videoInfo.loadedData.downloads[i]).Result;
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
                        }

                        //Extract downloaded file
                        Process process = new Process();
                        process.StartInfo.FileName = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip", "7z.exe");
                        process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPathRoot, "Launcher", "Resources", "7Zip");
                        process.StartInfo.Arguments = "x \"" + downloadedFilesList[0] + "\" -o\"" + (modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3]) + "\" -y";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        //Wait process finishes
                        process.WaitForExit();

                        //Move the final extracted video file
                        File.Move((modpackPathRoot + @"/Cache/" + urlParts[urlParts.Length - 3] + "/video.mp4"), (modpackPathRoot + @"/Downloads/" + intendedFileName));
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
            asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
            {
                //Get the thread response
                string threadTaskResponse = backgroundResult[0];

                //If have sucess, and the currently selected instance is this instance...
                if (threadTaskResponse == "success")
                    if (instantiatedByWindow.currentSelectedGameInstanceId == gameInstanceId)
                    {
                        //Start the video play
                        instantiatedByWindow.instanceBgVideo.Source = new Uri((modpackPath + @"/Downloads/instance-item-" + gameInstanceId + "-video.mp4"), UriKind.Absolute);
                        instantiatedByWindow.instanceBgVideo.Play();
                    }

                //Inform that is not loading the video anymore
                alreadyLoadingVideo = false;
            };
            asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
        }
    }
}
