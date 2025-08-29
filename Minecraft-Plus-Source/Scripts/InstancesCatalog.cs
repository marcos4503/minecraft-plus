using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Plus.Scripts
{
    /*
     * This class manage the load and save of instances catalog loaded from web
    */

    class InstancesCatalog
    {
        //Classes of script
        public class LoadedData
        {
            //*** Data to be saved ***//

            public GameInstance[] availableInstances = new GameInstance[0];
            public string catalogVersion = "";
        }

        //Private variables
        private string filePath = "";

        //Public variables
        public LoadedData loadedData = null;

        //Core methods

        public InstancesCatalog(string filePath)
        {
            //Check if save file exists
            bool saveExists = File.Exists(filePath);

            //Store the file path
            this.filePath = filePath;

            //If have a save file, load it
            if (saveExists == true)
                Load();
            //If a save file don't exists, create it
            if (saveExists == false)
                Save();
        }

        private void Load()
        {
            //Load the data
            string loadedDataString = File.ReadAllText(filePath);

            //Convert it to a loaded data object
            loadedData = JsonConvert.DeserializeObject<LoadedData>(loadedDataString);
        }

        //Public methods

        public void Save()
        {
            //If the loaded data is null, create one
            if (loadedData == null)
                loadedData = new LoadedData();

            //Save the data
            File.WriteAllText(filePath, JsonConvert.SerializeObject(loadedData));

            //Load the data to update loaded data
            Load();
        }
    }

    /*
     * Auxiliar classes
     * 
     * Classes that are objects that will be used, only to organize data inside 
     * "LoadedData" object in the saves.
    */

    public class GameInstance
    {
        public int instanceId = -1;
        public string instanceFolderName = "";
        public string instanceVersion = "";
        public string instanceTheme = "";
        public string instanceDescription = "";
        public int currentDataVersion = -1;
        public string requiresJava = "";
        public string requiredJavaUrl = "";
        public string iconUrl = "";
        public string backgroundVideoUrl = "";
        public string backgroundImageUrl = "";
        public string logoUrl = "";
        public TextFileToPatchInData[] textFilesToPatchInData = new TextFileToPatchInData[0];
        public string optimizedOptionsUrl = "";
        public string optimizedOptionsToApplyInGameUrl = "";
        public string crashReportsFolderPath = "";
        public string logsFolderPath = "";
        public string modsFolderPath = "";
        public string shaderPacksFolderPath = "";
        public string resourcePacksFolderPath = "";
        public string savesFolderPath = "";
        public string screenshotsFolderPath = "";
        public bool isUpdatableUsingSmartUpdater = false;
        public string[] smartUpdaterFoldersListToKeep = new string[0];
        public string[] smartUpdaterFilesListToKeep = new string[0];
        public string dataDownloadIndexUrl = "";
    }

    public class TextFileToPatchInData
    {
        public string fileTemplatePathInsideInstance = "";
        public KeyAndValueToPatchInTemplateFile[] keysAndValuesToPatchInTemplateFile = new KeyAndValueToPatchInTemplateFile[0];
        public string fileToBePatchedPathInsideInstance = "";
    }

    public class KeyAndValueToPatchInTemplateFile
    {
        public string key = "";
        public string type = "";
        public string value = "";
        public string[] contextOptionalStrings = new string[0];
    }
}
