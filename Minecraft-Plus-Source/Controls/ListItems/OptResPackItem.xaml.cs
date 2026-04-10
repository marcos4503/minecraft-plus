using System.IO;
using System.Windows;

namespace Minecraft_Plus.Controls.ListItems
{
    /*
     * This script is responsible by the work of the "OptResPackItem" that represents a Optional Resource
     * of a Resource Pack, that can be managed through the Optional Resources window.
    */

    public partial class OptResPackItem : System.Windows.Controls.UserControl
    {
        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnChangeActivation(bool isEnabledNow, int optResPackId);
        }

        //Private variables
        private event ClassDelegates.OnChangeActivation onChangeActivation;
        private int optionalResourcePackId = -1;

        //Public variables
        public WindowOptResources instantiatedByWindow = null;

        //Core methods

        public OptResPackItem(WindowOptResources instantiatedBy)
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

        public void SetID(int optionalResourcePackId)
        {
            //Store the ID
            this.optionalResourcePackId = optionalResourcePackId;
        }

        public void SetSource(string sourceEnabledPath, string sourceDisabledPath)
        {
            //If the some source, don't exists...
            if (File.Exists(sourceEnabledPath) == false || File.Exists(sourceDisabledPath) == false)
            {
                //Change the UI to error
                controlCbx.Visibility = System.Windows.Visibility.Collapsed;
                normalInfo.Visibility = System.Windows.Visibility.Collapsed;
                errorInfo.Visibility = System.Windows.Visibility.Visible;
            }
            //If the both source, exists...
            if (File.Exists(sourceEnabledPath) == true && File.Exists(sourceDisabledPath) == true)
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
                        this.onChangeActivation(false, optionalResourcePackId);
                if (controlCbx.SelectedIndex == 1)
                    if (this.onChangeActivation != null)
                        this.onChangeActivation(true, optionalResourcePackId);
            };
        }
    }
}
