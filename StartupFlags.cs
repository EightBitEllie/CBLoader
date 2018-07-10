﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace CBLoader
{
    [Serializable]
    [XmlRoot("Settings", IsNullable = false)]
    public sealed class SettingsFileSchema
    {
        [XmlArrayItem("Arg", IsNullable = false)]
        public string[] RawArgs { get; set; }
        [XmlArrayItem("Custom", IsNullable = false)]
        public string[] Folders { get; set; }
        [XmlArrayItem("Part", IsNullable = false)]
        public string[] Ignore { get; set; }

        public string BasePath { get; set; }
        public string CBPath { get; set; }
        public string KeyFile { get; set; }

        public bool FastMode { get; set; }
        public bool VerboseMode { get; set; }
        public bool AlwaysRemerge { get; set; }
        public bool UpdateFirst { get; set; }
        public bool LaunchBuilder { get; set; }
        public bool NewMergeLogic { get; set; }
        public bool ShowChangelog { get; set; }

        [XmlIgnore] public bool FastModeSpecified { get; set; }
        [XmlIgnore] public bool VerboseModeSpecified { get; set; }
        [XmlIgnore] public bool AlwaysRemergeSpecified { get; set; }
        [XmlIgnore] public bool UpdateFirstSpecified { get; set; }
        [XmlIgnore] public bool LaunchBuilderSpecified { get; set; }
        [XmlIgnore] public bool NewMergeLogicSpecified { get; set; }
        [XmlIgnore] public bool ShowChangelogSpecified { get; set; }
    }

    public sealed class StartupFlags
    {
        public const string CONFIG_FILENAME = "default.cbconfig";

        public List<string> Args { get; set; }
        public bool LoadExec { get; set; }
        public bool ForcedReload { get; set; }
        public bool Mergelater { get; set; }
        public bool UpdateFirst { get; set; }
        public bool CheckForUpdates { get; set; }

        private static readonly XmlSerializer configSerializer = new XmlSerializer(typeof(SettingsFileSchema));

        public StartupFlags()
        {
            Args = new List<string>();
            LoadExec = true;
            ForcedReload = false;
            Mergelater = false;
            UpdateFirst = false;
            CheckForUpdates = true;
        }

        /// <summary>
        /// Parses the command line arguments and sets the necessary state flags across the applicaiton.
        /// Returns a structure of startup flags to the caller.
        /// </summary>
        /// <returns>A structure containging flags important to how the application should load</returns>
        public bool ParseCmdArgs(string[] args, FileManager fm)
        {
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-e": this.ForcedReload = true; break;
                        case "-n": this.LoadExec = false; break;
                        case "-v": Log.VerboseMode = true; break;
                        case "-a": Utils.UpdateRegistry(); break;
                        case "-u": 
                            FileManager.BasePath = getArgString(args, ref i);
                            if (!Directory.Exists(FileManager.BasePath))
                                Directory.CreateDirectory(FileManager.BasePath);
                            break;
                        case "-r":
                            FileManager.KeyFile = getArgString(args, ref i);
                            Utils.ExtractKeyFile(FileManager.KeyFile);
                            break;
                        case "-k":
                            FileManager.KeyFile = getArgString(args, ref i);
                            break;
                        case "-f":
                            fm.AddCustomFolder(getArgString(args, ref i));
                            break;
                        case "-c": // Load a different config file.
                            LoadFromConfig(fm,getArgString(args, ref i));
                            break;
                        case "-?":
                        case "-h":
                            Program.DisplayHelp();
                            return false;
                        // Fast Mode
                        case "+fm":
                            this.Mergelater = File.Exists(FileManager.MergedPath); break;
                        case "-fm":
                            this.Mergelater = false; break;
                        case "+d":
                            this.UpdateFirst = true;
                            this.LoadExec = false;
                            break;
                        case "-d":
                            CheckForUpdates = false;
                            break;
                        default:
                            Args.Add(args[i]);
                            break;
                    }
                }
            }
            return true;
        }



        /// <summary>
        /// simple helper for safely pulling a string argument out of an args list
        /// </summary>
        private static string getArgString(string[] args, ref int i)
        {
            if (args.Length > i + 1)
                return args[++i];
            else
            {
                Program.DisplayHelp();
                throw new FormatException("Invalid Arguments Specified");
            }
        }

        public bool LoadFromConfig(FileManager fm)
        {
            return LoadFromConfig(fm, CONFIG_FILENAME);
        }
        // The whole point of not using app.config was that we could have more than one.
        public bool LoadFromConfig(FileManager fm, string ConfigFile)
        {
            string fileName;
            fileName = ConfigFile;
            if (!File.Exists(fileName))
                fileName = Path.Combine(FileManager.BasePath, ConfigFile);
            if (!File.Exists(fileName))
                fileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),ConfigFile);
            if (!File.Exists(fileName))
                return false;

            Log.Debug("Loading Config File: " + fileName);
            try
            {
                SettingsFileSchema settings;
                using (StreamReader sr = new StreamReader(fileName))
                {
                    settings = (SettingsFileSchema)configSerializer.Deserialize(sr);
                }
                if (settings.Folders != null)
                    foreach (string customFolder in settings.Folders)
                        fm.AddCustomFolder(customFolder);
                if (settings.Ignore != null)
                    foreach (string ignoredPart in settings.Ignore)
                        fm.IgnoredParts.Add(ignoredPart.ToLower().Trim());
                if (settings.AlwaysRemergeSpecified)
                    this.ForcedReload = settings.AlwaysRemerge;
                if (!String.IsNullOrEmpty(settings.BasePath))
                {
                    FileManager.BasePath = Environment.ExpandEnvironmentVariables(settings.BasePath);
                    if (!Directory.Exists(FileManager.BasePath))
                        Directory.CreateDirectory(FileManager.BasePath);
                }
                if (!String.IsNullOrEmpty(settings.CBPath))
                    Environment.CurrentDirectory = Environment.ExpandEnvironmentVariables(settings.CBPath);
                if (settings.FastModeSpecified)
                    this.Mergelater = settings.FastMode;
                if (!String.IsNullOrEmpty(settings.KeyFile))
                    FileManager.KeyFile = Environment.ExpandEnvironmentVariables(settings.KeyFile);
                if (settings.VerboseModeSpecified)
                    Log.VerboseMode = settings.VerboseMode;
                if (settings.UpdateFirstSpecified)
                    this.UpdateFirst = settings.UpdateFirst;
                if (settings.LaunchBuilderSpecified)
                    this.LoadExec = settings.LaunchBuilder;
                if (settings.NewMergeLogicSpecified)
                    fm.UseNewMergeLogic = settings.NewMergeLogic;
                if (settings.ShowChangelogSpecified)
                    UpdateLog.ShowChangelog = settings.ShowChangelog;
                if (settings.RawArgs != null)
                    if (!ParseCmdArgs(settings.RawArgs, fm))
                        return false;
            }
            catch (Exception e)
            {
                Log.Error("Error Loading Config File", e);
                return false;
            }
            return true;
        }
    }
}
