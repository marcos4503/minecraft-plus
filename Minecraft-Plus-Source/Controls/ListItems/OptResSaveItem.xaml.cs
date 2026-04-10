using System.IO;
using System.Windows;

namespace Minecraft_Plus.Controls.ListItems
{
    /*
     * This script is responsible by the work of the "OptResSaveItem" that represents a Optional Resource
     * of a Save, that can be managed through the Optional Resources window.
    */

    public partial class OptResSaveItem : System.Windows.Controls.UserControl
    {
        //Public enums of script
        public enum UiMode
        {
            Import,
            AlreadyImported
        }

        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnImport(int optSaveId);
        }

        //Private variables
        private event ClassDelegates.OnImport onImport;
        private int optionalResourceSaveId = -1;

        //Public variables
        public WindowOptResources instantiatedByWindow = null;

        //Core methods

        public OptResSaveItem(WindowOptResources instantiatedBy)
        {
            //Initialize the component
            InitializeComponent();

            //Inform that is the DataConext of this User Control
            this.DataContext = this;

            //Store reference for window that was instantiated this item
            this.instantiatedByWindow = instantiatedBy;
        }

        //Public methods

        public void SetTitle(string title)
        {
            //Set the title
            this.title.Text = title;
        }

        public void SetDescription(string description)
        {
            //Prepare the Tooltip
            System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock();
            textBlock.Text = description;
            textBlock.TextWrapping = TextWrapping.Wrap;
            System.Windows.Controls.ToolTip toolTip = new System.Windows.Controls.ToolTip();
            toolTip.Content = textBlock;
            toolTip.MaxWidth = 350;
            //Set the description
            this.normalInfo.ToolTip = toolTip;
        }

        public void SetID(int optionalResourceSaveId)
        {
            //Store the ID
            this.optionalResourceSaveId = optionalResourceSaveId;
        }

        public void SetSourceZip(string sourceZipPath)
        {
            //If the source jar don't exists...
            if (File.Exists(sourceZipPath) == false)
            {
                //Change the UI to error
                alreadyImportedBtn.Visibility = Visibility.Collapsed;
                importBtn.Visibility = Visibility.Collapsed;
                normalInfo.Visibility = System.Windows.Visibility.Collapsed;
                errorInfo.Visibility = System.Windows.Visibility.Visible;

            }
            //If the source jar exists...
            if (File.Exists(sourceZipPath) == true)
            {
                //Change the UI to normal
                alreadyImportedBtn.Visibility = Visibility.Collapsed;
                importBtn.Visibility = Visibility.Collapsed;
                normalInfo.Visibility = System.Windows.Visibility.Visible;
                errorInfo.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        public void RegisterOnImport(ClassDelegates.OnImport onImport)
        {
            //Register the event
            this.onImport = onImport;

            //Run the callback on click
            importBtn.Click += (s, e) =>
            {
                //Run the callback of import
                if (this.onImport != null)
                    this.onImport(optionalResourceSaveId);
            };
        }

        //Public auxiliar methods

        public void SetUiMode(UiMode newUiMode)
        {
            //If is desired the UI Mode of Import
            if (newUiMode == UiMode.Import)
            {
                alreadyImportedBtn.Visibility = Visibility.Collapsed;
                importBtn.Visibility = Visibility.Visible;
            }
            //If is desired the UI Mode of Import Not Available
            if (newUiMode == UiMode.AlreadyImported)
            {
                alreadyImportedBtn.Visibility = Visibility.Visible;
                importBtn.Visibility = Visibility.Collapsed;
            }
        }
    }
}
