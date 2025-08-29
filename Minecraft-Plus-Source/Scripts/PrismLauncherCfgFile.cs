using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Plus.Scripts
{
    /*
     * This class represents and manager a prism launcher CFG file
    */

    public class PrismLauncherCfgFile
    {
        //Private variables
        private string filePath = "";
        private Dictionary<string, string> cfgLines = new Dictionary<string, string>();

        //Core methods

        public PrismLauncherCfgFile(string filePath)
        {
            //Fill the dictionary
            foreach(string line in File.ReadAllLines(filePath))
            {
                //If line contains "[General]", ignore
                if (line.Contains("[General]") == true)
                    continue;
                //If the line don't contains key and value, ignore
                if (line.Contains("=") == false)
                    continue;

                //Split the line
                string[] lineSplitted = line.Split(new[] { '=' }, 2);
                string key = lineSplitted[0].Replace(" ", "");
                string value = lineSplitted[1];

                //Add it to dictionary
                if (cfgLines.ContainsKey(key) == false)
                    cfgLines.Add(key, value);
            }

            //Store the file path
            this.filePath = filePath;
        }
    
        public void UpdateValue(string key, string value)
        {
            //Update the value, if exists
            if (cfgLines.ContainsKey(key) == true)
                cfgLines[key] = value;
        }
    
        public void Save()
        {
            //Prepare a list of lines of CFG file
            List<string> saveLines = new List<string>();

            //Add the header of the file
            saveLines.Add("[General]");

            //Add all lines of dictionary
            foreach (var key in cfgLines)
                saveLines.Add((key.Key + "=" + key.Value));

            //Save the file
            File.WriteAllLines(filePath, saveLines.ToArray());
        }
    }
}
