using CoroutinesDotNet;
using CoroutinesForWpf;
using Minecraft_Plus.Controls.ListItems;
using Minecraft_Plus.Scripts;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Minecraft_Plus
{
    /*
     * This is the script responsible by the Optional Resources manager for instances in Launcher.
    */

    public partial class WindowOptResources : Window
    {
        //Private cache variables
        private bool isFirstLoadedCurrentSituationOfTheInstanceToUi = false;
        private GameInstance currentGameInstanceDataFromCatalog = null;
        private List<OptModItem> instantiatedOptModItems = new List<OptModItem>();
        private List<OptResPackItem> instantiatedOptResPackItems = new List<OptResPackItem>();
        private List<OptResSaveItem> instantiatedOptSaveItems = new List<OptResSaveItem>();
        private IDisposable showContentSavedFeedbackRoutine = null;

        //Private variables
        private MainWindow mainWindowRef = null;
        private string modpackPath = "";
        private int currentSelectedInstanceId = -1;

        //Core methods

        public WindowOptResources(MainWindow mainWindow, string modpackPath, int currentSelectedInstanceId)
        {
            //Initialize the Window
            InitializeComponent();

            //Store the data
            this.mainWindowRef = mainWindow;
            this.modpackPath = modpackPath;
            this.currentSelectedInstanceId = currentSelectedInstanceId;

            //Prepare the UI
            PrepareTheUI();
        }

        private void PrepareTheUI()
        {
            //Parse the downloaded instances catalog, and get the Game Instance data of the current selected instance
            currentGameInstanceDataFromCatalog = new InstancesCatalog((modpackPath + @"/Downloads/instances-catalog.json")).loadedData.availableInstances[currentSelectedInstanceId];

            //Prepare the Title
            optResTitle.Text = ("Recursos Opcionais para " + currentGameInstanceDataFromCatalog.instanceTheme + " - v" + currentGameInstanceDataFromCatalog.instanceVersion);

            //Prepare the max RAM allocation selector
            ramAllocCbx.SelectionChanged += (s, e) =>
            {
                //Only run the code, if have runned the first load (this avoid a false positive code run, when the user not changed the combo box, but the code of change, is runned)
                if (isFirstLoadedCurrentSituationOfTheInstanceToUi == true)
                    Coroutine.Start(ApplyNewRamAllocationRoutine());
            };
            //Prepare the Instance Note save
            this.Closing += (s, e) =>
            {
                //Try to found a Instance Note saved for this Instance
                bool wasFoundSavedInstanceNoteForThis = false;
                foreach (InstanceNote note in mainWindowRef.preferences.loadedData.instancesNotes)
                    if (note.instanceId == currentSelectedInstanceId)
                        wasFoundSavedInstanceNoteForThis = true;
                //If not found, add a new Instance Note object to the save file
                if (wasFoundSavedInstanceNoteForThis == false)
                {
                    List<InstanceNote> instanceNotes = new List<InstanceNote>();
                    foreach (InstanceNote note in mainWindowRef.preferences.loadedData.instancesNotes)
                        instanceNotes.Add(note);
                    InstanceNote newInstanceNote = new InstanceNote();
                    newInstanceNote.instanceId = currentSelectedInstanceId;
                    newInstanceNote.note = "";
                    instanceNotes.Add(newInstanceNote);
                    mainWindowRef.preferences.loadedData.instancesNotes = instanceNotes.ToArray();
                }
                //Update the saved Instance Note in the save file
                foreach (InstanceNote note in mainWindowRef.preferences.loadedData.instancesNotes)
                    if (note.instanceId == currentSelectedInstanceId)
                        note.note = instanceNotesTxt.Text;
                //Save the preferences file
                mainWindowRef.preferences.Save();
            };
            //Render a component to handle each Optional Resource informed in the catalog
            InstantiateAllOptionalResources();

            //Disable the save feedback message
            contentSavedWarn.Visibility = Visibility.Collapsed;

            //Update the UI to reflect the current situation of the Game Instance
            LoadCurrentSituationOfTheInstanceToUI();
            //Inform that was runned the first load of the current situation of the Game Instance
            isFirstLoadedCurrentSituationOfTheInstanceToUi = true;
        }

        private void InstantiateAllOptionalResources()
        {
            //Instatiate each Optional Resource of Mod
            bool isFirstOptionalResourceModAdded = false;
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionalResourcesMods.Length; i++)
            {
                //Get data
                OptResMod modItem = currentGameInstanceDataFromCatalog.optionalResourcesMods[i];

                //Disable the empty warn
                optModsEmpty.Visibility = Visibility.Collapsed;
                //Instantiate and store reference for it
                OptModItem newOptModItem = new OptModItem(this);
                optModsList.Children.Add(newOptModItem);
                instantiatedOptModItems.Add(newOptModItem);
                //Set it up
                newOptModItem.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                newOptModItem.VerticalAlignment = VerticalAlignment.Top;
                newOptModItem.Width = double.NaN;
                newOptModItem.Height = double.NaN;
                if (isFirstOptionalResourceModAdded == false)
                    newOptModItem.Margin = new Thickness(0, 0, 0, 0);
                if (isFirstOptionalResourceModAdded == true)
                    newOptModItem.Margin = new Thickness(0, 8, 0, 0);
                //Fill this item
                newOptModItem.SetTitle(modItem.displayName);
                newOptModItem.SetDescription(modItem.displayDescription);
                newOptModItem.SetID(i);
                newOptModItem.SetSourceJar((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + modItem.pathToFindSourceJar));
                //Set the code to run on change the activation
                newOptModItem.RegisterOnChangeActivation((isEnabledNow, optModId) =>
                {
                    //Only run the code, if have runned the first load (this avoid a false positive code run, when the user not changed the combo box, but the code of change, is runned)
                    if (isFirstLoadedCurrentSituationOfTheInstanceToUi == true)
                        Coroutine.Start(ModifyOptionalResource_Mod(isEnabledNow, optModId));
                });

                //Inform that the first Optional Resource mod was added
                isFirstOptionalResourceModAdded = true;
            }



            //Instantiate each Optional Resource of Resource Pack
            bool isFirstOptionalResourceResourcePackAdded = false;
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionResourcesResourcePacks.Length; i++)
            {
                //Get data
                OptResResourcePack rpItem = currentGameInstanceDataFromCatalog.optionResourcesResourcePacks[i];

                //Disable the empty warn
                optResPacksEmpty.Visibility = Visibility.Collapsed;
                //Instantiate and store reference for it
                OptResPackItem newOptResPackItem = new OptResPackItem(this);
                optResPacksList.Children.Add(newOptResPackItem);
                instantiatedOptResPackItems.Add(newOptResPackItem);
                //Set it up
                newOptResPackItem.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                newOptResPackItem.VerticalAlignment = VerticalAlignment.Top;
                newOptResPackItem.Width = double.NaN;
                newOptResPackItem.Height = double.NaN;
                if (isFirstOptionalResourceResourcePackAdded == false)
                    newOptResPackItem.Margin = new Thickness(0, 0, 0, 0);
                if (isFirstOptionalResourceResourcePackAdded == true)
                    newOptResPackItem.Margin = new Thickness(0, 8, 0, 0);
                //Fill this item
                newOptResPackItem.SetTitle(rpItem.displayName);
                newOptResPackItem.SetDescription(rpItem.displayDescription);
                newOptResPackItem.SetID(i);
                newOptResPackItem.SetSource((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + rpItem.pathToFindSourceEnabledResourcePack),
                                            (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + rpItem.pathToFindSourceDisabledResourcePack));
                //Set the code to run on change the activation
                newOptResPackItem.RegisterOnChangeActivation((isEnabledNow, optResPackId) =>
                {
                    //Only run the code, if have runned the first load (this avoid a false positive code run, when the user not changed the combo box, but the code of change, is runned)
                    if (isFirstLoadedCurrentSituationOfTheInstanceToUi == true)
                        Coroutine.Start(ModifyOptionalResource_ResourcePack(isEnabledNow, optResPackId));
                });

                //Inform that the first Optional Resource mod was added
                isFirstOptionalResourceResourcePackAdded = true;
            }



            //Instantiate each Optiona Resource of Save
            bool isFirstOptionalResourceSaveAdded = false;
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionalResourcesSaves.Length; i++)
            {
                //Get data
                OptResSaves saveItem = currentGameInstanceDataFromCatalog.optionalResourcesSaves[i];

                //Disable the empty warn
                optSavesEmpty.Visibility = Visibility.Collapsed;
                //Instantiate and store reference for it
                OptResSaveItem newOptResSaveItem = new OptResSaveItem(this);
                optSavesList.Children.Add(newOptResSaveItem);
                instantiatedOptSaveItems.Add(newOptResSaveItem);
                //Set it up
                newOptResSaveItem.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                newOptResSaveItem.VerticalAlignment = VerticalAlignment.Top;
                newOptResSaveItem.Width = double.NaN;
                newOptResSaveItem.Height = double.NaN;
                if (isFirstOptionalResourceSaveAdded == false)
                    newOptResSaveItem.Margin = new Thickness(0, 0, 0, 0);
                if (isFirstOptionalResourceSaveAdded == true)
                    newOptResSaveItem.Margin = new Thickness(0, 8, 0, 0);
                //Fill this item
                newOptResSaveItem.SetTitle(saveItem.displayName);
                newOptResSaveItem.SetDescription(saveItem.displayDescription);
                newOptResSaveItem.SetID(i);
                newOptResSaveItem.SetSourceZip((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + saveItem.pathToFindCompressedSave));
                //Set the code to run on change the import
                newOptResSaveItem.RegisterOnImport((optSaveId) =>
                {
                    //Only run the code, if have runned the first load (this avoid a false positive code run, when the user not changed the combo box, but the code of change, is runned)
                    if (isFirstLoadedCurrentSituationOfTheInstanceToUi == true)
                        Coroutine.Start(ModifyOptionalResource_Save(optSaveId));
                });

                //Inform that the first Optional Resource mod was added
                isFirstOptionalResourceSaveAdded = true;
            }
        }

        private void LoadCurrentSituationOfTheInstanceToUI()
        {
            //Load the "instance.cfg" of the current instance
            string[] instanceFileLines = File.ReadAllLines((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/instance.cfg"));
            //Set the default state of the RAM selector
            ramAllocCbx.SelectedIndex = 0;
            ramAllocCbx.IsEnabled = false;
            //Load the current max RAM alloc to UI
            foreach (string line in instanceFileLines)
                if (line.Replace(" ", "").Contains("MaxMemAlloc=") == true)
                {
                    //Get the raw line
                    string rawValue = line.Split("=")[1];
                    //Try to detect the current value
                    if (rawValue == "2048")
                    {
                        ramAllocCbx.SelectedIndex = 1;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "2560")
                    {
                        ramAllocCbx.SelectedIndex = 2;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "3072")
                    {
                        ramAllocCbx.SelectedIndex = 3;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "4096")
                    {
                        ramAllocCbx.SelectedIndex = 4;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "8192" || rawValue == "8096")
                    {
                        ramAllocCbx.SelectedIndex = 5;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "12288")
                    {
                        ramAllocCbx.SelectedIndex = 6;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "16384")
                    {
                        ramAllocCbx.SelectedIndex = 7;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "24576")
                    {
                        ramAllocCbx.SelectedIndex = 8;
                        ramAllocCbx.IsEnabled = true;
                    }
                    if (rawValue == "32768")
                    {
                        ramAllocCbx.SelectedIndex = 9;
                        ramAllocCbx.IsEnabled = true;
                    }
                }

            //Set the default state of Instance Notes
            instanceNotesTxt.Text = "";
            //Load the Instance Notes
            foreach (InstanceNote note in mainWindowRef.preferences.loadedData.instancesNotes)
                if (note.instanceId == currentSelectedInstanceId)
                    instanceNotesTxt.Text = note.note;

            //Render current state of each Optional Mod
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionalResourcesMods.Length; i++)
            {
                //Stop here if this mod is not editable...
                if (instantiatedOptModItems[i].errorInfo.Visibility != Visibility.Collapsed)
                    continue;

                //Prepare data
                OptResMod currentOptResModData = currentGameInstanceDataFromCatalog.optionalResourcesMods[i];
                string fullPathToFindCurrentSourceJar = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResModData.pathToFindSourceJar);
                string fullPathToPutIfIsEnabled = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResModData.pathToPutIfIsEnabled);
                fullPathToPutIfIsEnabled = fullPathToPutIfIsEnabled.Replace("{SOURCE_JAR_NAME}", System.IO.Path.GetFileName(fullPathToFindCurrentSourceJar));

                //If this mod already exists in the Mods folder, mark it as enabled
                if (File.Exists(fullPathToPutIfIsEnabled) == true)
                    instantiatedOptModItems[i].controlCbx.SelectedIndex = 1;
                //If this mod not exists, in the Mods folder, mark as disabled
                if (File.Exists(fullPathToPutIfIsEnabled) == false)
                    instantiatedOptModItems[i].controlCbx.SelectedIndex = 0;
            }

            //Render current state of each Optional Resource Pack
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionResourcesResourcePacks.Length; i++)
            {
                //Stop here if this resource pack is not editable...
                if (instantiatedOptResPackItems[i].errorInfo.Visibility != Visibility.Collapsed)
                    continue;

                //Prepare data
                OptResResourcePack currentOptResRpackData = currentGameInstanceDataFromCatalog.optionResourcesResourcePacks[i];
                string fullPathToFindSourceEnabledResourcePack = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResRpackData.pathToFindSourceEnabledResourcePack);
                string fullPathToFindSourceDisabledResourcePack = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResRpackData.pathToFindSourceDisabledResourcePack);
                string fullPathToPutWhenPlacing = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResRpackData.pathToPutWhenPlacing);

                //If the size of the already placed Resource Pack, is equal to the Source ON...
                if ((new FileInfo(fullPathToPutWhenPlacing).Length) == (new FileInfo(fullPathToFindSourceEnabledResourcePack).Length))
                    instantiatedOptResPackItems[i].controlCbx.SelectedIndex = 1;
                //If the size of the already placed Resource Pack, is equal to the Source OFF...
                if ((new FileInfo(fullPathToPutWhenPlacing).Length) == (new FileInfo(fullPathToFindSourceDisabledResourcePack).Length))
                    instantiatedOptResPackItems[i].controlCbx.SelectedIndex = 0;
            }

            //Render current state of each Optional Save
            for (int i = 0; i < currentGameInstanceDataFromCatalog.optionalResourcesSaves.Length; i++)
            {
                //Stop here if this save is not editable...
                if (instantiatedOptSaveItems[i].errorInfo.Visibility != Visibility.Collapsed)
                    continue;

                //Prepare data
                OptResSaves currentOptResSaveData = currentGameInstanceDataFromCatalog.optionalResourcesSaves[i];
                string fullPathWhenAlreadyIsPlacedAndDecompressed = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + currentOptResSaveData.pathWhenAlreadyIsPlacedAndDecompressed);

                //If the directory of this placed and decompressed save, already exists
                if (Directory.Exists(fullPathWhenAlreadyIsPlacedAndDecompressed) == true)
                    instantiatedOptSaveItems[i].SetUiMode(OptResSaveItem.UiMode.AlreadyImported);
                if (Directory.Exists(fullPathWhenAlreadyIsPlacedAndDecompressed) == false)
                    instantiatedOptSaveItems[i].SetUiMode(OptResSaveItem.UiMode.Import);
            }
        }

        //Private auxiliar methods

        private void ShowContentSavedFeedbackMessage()
        {
            //If already have a running save message routine, stop it
            if (showContentSavedFeedbackRoutine != null)
            {
                showContentSavedFeedbackRoutine.Dispose();
                showContentSavedFeedbackRoutine = null;
            }

            //Run the message of content saved feedback
            showContentSavedFeedbackRoutine = Coroutine.Start(ShowContentSavedFeedbackMessageRoutine());
        }

        private IEnumerator ShowContentSavedFeedbackMessageRoutine()
        {
            //Disable the save message
            contentSavedWarn.Visibility = Visibility.Collapsed;

            //Wait a bit
            yield return new WaitForSeconds(0.5f);

            //Enable the feedback message
            contentSavedWarn.Visibility = Visibility.Visible;

            //Wait the time before disable it
            yield return new WaitForSeconds(5.0f);

            //Disable the message
            contentSavedWarn.Visibility = Visibility.Collapsed;
            //Clear the routine reference
            showContentSavedFeedbackRoutine = null;
        }

        private IEnumerator ApplyNewRamAllocationRoutine()
        {
            //Enable the interaction blocker
            interactionBlocker.Visibility = Visibility.Visible;

            //Wait time before start
            yield return new WaitForSeconds(0.25f);

            //Load the "instance.cfg" of the current instance
            string[] instanceFileLines = File.ReadAllLines((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/instance.cfg"));
            //Ensure the enable of the Memory Override
            for (int i = 0; i < instanceFileLines.Length; i++)
            {
                //Get current line
                string currentLine = instanceFileLines[i];

                //If this line not handle the Memory Override, stop here
                if (currentLine.Replace(" ", "").Contains("OverrideMemory=") == false)
                    continue;

                //Apply the new value
                currentLine = "OverrideMemory=true";

                //Apply the edit to the original line
                instanceFileLines[i] = currentLine;
            }
            //Set the current max RAM alloc of UI
            for (int i = 0; i < instanceFileLines.Length; i++)
            {
                //Get current line
                string currentLine = instanceFileLines[i];

                //If this line not handle the RAM alloc, stop here
                if (currentLine.Replace(" ", "").Contains("MaxMemAlloc=") == false)
                    continue;

                //Apply the new value
                if (ramAllocCbx.SelectedIndex == 1)
                    currentLine = "MaxMemAlloc=2048";
                if (ramAllocCbx.SelectedIndex == 2)
                    currentLine = "MaxMemAlloc=2560";
                if (ramAllocCbx.SelectedIndex == 3)
                    currentLine = "MaxMemAlloc=3072";
                if (ramAllocCbx.SelectedIndex == 4)
                    currentLine = "MaxMemAlloc=4096";
                if (ramAllocCbx.SelectedIndex == 5)
                    currentLine = "MaxMemAlloc=8192";
                if (ramAllocCbx.SelectedIndex == 6)
                    currentLine = "MaxMemAlloc=12288";
                if (ramAllocCbx.SelectedIndex == 7)
                    currentLine = "MaxMemAlloc=16384";
                if (ramAllocCbx.SelectedIndex == 8)
                    currentLine = "MaxMemAlloc=24576";
                if (ramAllocCbx.SelectedIndex == 9)
                    currentLine = "MaxMemAlloc=32768";

                //Apply the edit to the original line
                instanceFileLines[i] = currentLine;
            }
            //Save the "instance.cfg" of the current instance, now edited
            File.WriteAllLines((modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/instance.cfg"), instanceFileLines);

            //Wait time before end
            yield return new WaitForSeconds(0.25f);

            //Disable the interaction blocker
            interactionBlocker.Visibility = Visibility.Collapsed;

            //Show the successfully save feedback message
            ShowContentSavedFeedbackMessage();
        }

        private IEnumerator ModifyOptionalResource_Mod(bool isEnabledNow, int modId)
        {
            //Enable the interaction blocker
            interactionBlocker.Visibility = Visibility.Visible;

            //Wait time before start
            yield return new WaitForSeconds(0.25f);

            //Load data about the requested Optional Resource of Mod
            OptResMod modItem = currentGameInstanceDataFromCatalog.optionalResourcesMods[modId];
            string fullPathToFindCurrentSourceJar = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + modItem.pathToFindSourceJar);
            string fullPathToPutIfIsEnabled = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + modItem.pathToPutIfIsEnabled);
            fullPathToPutIfIsEnabled = fullPathToPutIfIsEnabled.Replace("{SOURCE_JAR_NAME}", System.IO.Path.GetFileName(fullPathToFindCurrentSourceJar));

            //If is desired to enable...
            if (isEnabledNow == true)
                if (File.Exists(fullPathToPutIfIsEnabled) == false)
                    File.Copy(fullPathToFindCurrentSourceJar, fullPathToPutIfIsEnabled);

            //If is desired to disable...
            if (isEnabledNow == false)
                if (File.Exists(fullPathToPutIfIsEnabled) == true)
                    File.Delete(fullPathToPutIfIsEnabled);

            //Wait time before end
            yield return new WaitForSeconds(0.25f);

            //Disable the interaction blocker
            interactionBlocker.Visibility = Visibility.Collapsed;

            //Show the successfully save feedback message
            ShowContentSavedFeedbackMessage();
        }

        private IEnumerator ModifyOptionalResource_ResourcePack(bool isEnabledNow, int resourcePackId)
        {
            //Enable the interaction blocker
            interactionBlocker.Visibility = Visibility.Visible;

            //Wait time before start
            yield return new WaitForSeconds(0.25f);

            //Load data about the requested Optional Resource of Resource Pack
            OptResResourcePack rpItem = currentGameInstanceDataFromCatalog.optionResourcesResourcePacks[resourcePackId];
            string fullPathToFindSourceEnabledResourcePack = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + rpItem.pathToFindSourceEnabledResourcePack);
            string fullPathToFindSourceDisabledResourcePack = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + rpItem.pathToFindSourceDisabledResourcePack);
            string fullPathToPutWhenPlacing = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + rpItem.pathToPutWhenPlacing);

            //If already exists a copy placed, delete it
            if (File.Exists(fullPathToPutWhenPlacing) == true)
                File.Delete(fullPathToPutWhenPlacing);

            //If is desired to enable...
            if (isEnabledNow == true)
                File.Copy(fullPathToFindSourceEnabledResourcePack, fullPathToPutWhenPlacing);
            //If is desired to disable...
            if (isEnabledNow == false)
                File.Copy(fullPathToFindSourceDisabledResourcePack, fullPathToPutWhenPlacing);

            //Wait time before end
            yield return new WaitForSeconds(0.25f);

            //Disable the interaction blocker
            interactionBlocker.Visibility = Visibility.Collapsed;

            //Show the successfully save feedback message
            ShowContentSavedFeedbackMessage();
        }

        private IEnumerator ModifyOptionalResource_Save(int saveId)
        {
            //Enable the interaction blocker
            interactionBlocker.Visibility = Visibility.Visible;

            //Wait time before start
            yield return new WaitForSeconds(0.25f);

            //Load data about the requested Optional Resource of Save
            OptResSaves saveItem = currentGameInstanceDataFromCatalog.optionalResourcesSaves[saveId];
            string fullPathToFindCompressedSave = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + saveItem.pathToFindCompressedSave).Replace(@"//", @"/");
            string fullpathToPutAndDecompress = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + saveItem.pathToPutAndDecompress).Replace(@"//", @"/");
            string fullPathWhenAlreadyIsPlacedAndDecompressed = (modpackPath + @"/Game/instances" + currentGameInstanceDataFromCatalog.instanceFolderName + @"/" + saveItem.pathWhenAlreadyIsPlacedAndDecompressed);

            //Decompress the Save in the target path
            Process process = new Process();
            process.StartInfo.FileName = System.IO.Path.Combine(modpackPath, "Launcher", "Resources", "7Zip", "7z.exe");
            process.StartInfo.WorkingDirectory = System.IO.Path.Combine(modpackPath, "Launcher", "Resources", "7Zip");
            process.StartInfo.Arguments = "x \"" + fullPathToFindCompressedSave + "\" -o\"" + fullpathToPutAndDecompress + "\" -y";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;  //<- Hide the process window
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            //Wait process finishes
            process.WaitForExit();

            //If the directory of this placed and decompressed save, already exists
            if (Directory.Exists(fullPathWhenAlreadyIsPlacedAndDecompressed) == true)
                instantiatedOptSaveItems[saveId].SetUiMode(OptResSaveItem.UiMode.AlreadyImported);
            if (Directory.Exists(fullPathWhenAlreadyIsPlacedAndDecompressed) == false)
                instantiatedOptSaveItems[saveId].SetUiMode(OptResSaveItem.UiMode.Import);

            //Wait time before end
            yield return new WaitForSeconds(0.25f);

            //Disable the interaction blocker
            interactionBlocker.Visibility = Visibility.Collapsed;

            //Show the successfully save feedback message
            ShowContentSavedFeedbackMessage();
        }
    }
}