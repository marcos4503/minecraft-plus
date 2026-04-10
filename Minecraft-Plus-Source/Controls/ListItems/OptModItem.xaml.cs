using System.IO;
using System.Windows;

namespace Minecraft_Plus.Controls.ListItems
{
    /*
     * This script is responsible by the work of the "OptModItem" that represents a Optional Resource
     * of a Mod, that can be managed through the Optional Resources window.
    */

    public partial class OptModItem : System.Windows.Controls.UserControl
    {
        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnChangeActivation(bool isEnabledNow, int optModId);
        }

        //Private variables
        private event ClassDelegates.OnChangeActivation onChangeActivation;
        private int optionalResourceModId = -1;

        //Public variables
        public WindowOptResources instantiatedByWindow = null;

        //Core methods

        public OptModItem(WindowOptResources instantiatedBy)
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

        public void SetID(int optionalResourceModId)
        {
            //Store the ID
            this.optionalResourceModId = optionalResourceModId;
        }

        public void SetSourceJar(string sourceJarPath)
        {
            //If the source jar don't exists...
            if (File.Exists(sourceJarPath) == false)
            {
                //Change the UI to error
                controlCbx.Visibility = System.Windows.Visibility.Collapsed;
                normalInfo.Visibility = System.Windows.Visibility.Collapsed;
                errorInfo.Visibility = System.Windows.Visibility.Visible;
            }
            //If the source jar exists...
            if (File.Exists(sourceJarPath) == true)
            {
                //Change the UI to normal
                controlCbx.Visibility = System.Windows.Visibility.Visible;
                normalInfo.Visibility = System.Windows.Visibility.Visible;
                errorInfo.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        public void RegisterOnChangeActivation(ClassDelegates.OnChangeActivation onChangeActivation)
        {
            //Register the event
            this.onChangeActivation = onChangeActivation;

            //Register the callback in the buttons
            controlCbx.SelectionChanged += (s, e) =>
            {
                //Run the callback of change activation
                if (controlCbx.SelectedIndex == 0)
                    if (this.onChangeActivation != null)
                        this.onChangeActivation(false, optionalResourceModId);
                if (controlCbx.SelectedIndex == 1)
                    if (this.onChangeActivation != null)
                        this.onChangeActivation(true, optionalResourceModId);
            };
        }
    }
}