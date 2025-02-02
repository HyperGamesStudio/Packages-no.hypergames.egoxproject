﻿// ------------------------------------------
//   EgoXproject
//   Copyright © 2013-2019 Egomotion Limited
// ------------------------------------------

using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

using UnityEngine;

namespace Egomotion.EgoXproject.Internal
{
    internal class XcodeChangeFile
    {
        const string TYPE_KEY = "Type";
        const string TYPE_VALUE = "EgoXproject Change File";

        const string VERSION_KEY = "Version";
        const string BUILD_PLATFORM_KEY = "BuildPlatform";
        const string FRAMEWORKS_KEY = "Frameworks";
        const string SCRIPTS_KEY = "Scripts";
        const string FILES_AND_FOLDERS_KEY = "FilesAndFolders";
        const string BUILD_SETTINGS_KEY = "BuildSettings";
        const string APP_CONFIG_KEY = "AppConfig";
        const string APP_CONFIG_ENABLED_KEY = "AppConfigEnabled";
        const string INFO_PLIST_KEY = "InfoPlist";
        const string MANUAL_ENTITLEMENTS = "ManualEntitlements";
        const string SIGNING_KEY = "Signing";
        const string CAPABILITIES_KEY = "Capabilities";
        const string LANGUAGES_DICT_KEY = "EnableLanguages";

        const int VERSION = 2;
        readonly int[] _supportedVersions = { 1 };

        public string SavePath { get; private set; }

        public BuildPlatform Platform { get; set; }

        public static string Extension = ".egoxc";

        public FrameworkChanges Frameworks { get; private set; }

        public FilesAndFolderChanges FilesAndFolders { get; private set; }

        public ScriptChanges Scripts { get; private set; }

        public BuildSettingsChanges BuildSettings { get; private set; }

        public PListDictionary AppConfig { get; set; }
        
        public PListArray AppConfigEnabled { get; set; }
        
        public PListDictionary InfoPlistChanges { get; set; }
        
        public PListDictionary ManualEntitlements { get; set; }

        public SigningChanges Signing { get; private set; }

        public CapabilitiesChanges Capabilities { get; private set; }
        

        public XcodeChangeFile()
        {
            IsDirty = false;
            SavePath = "";
            Platform = BuildPlatform.iOS;
            Frameworks = new FrameworkChanges();
            FilesAndFolders = new FilesAndFolderChanges();
            Scripts = new ScriptChanges();
            BuildSettings = new BuildSettingsChanges();
            InfoPlistChanges = new PListDictionary();
            AppConfig = new PListDictionary();
            AppConfigEnabled = new PListArray();
            ManualEntitlements = new PListDictionary();
            Signing = new SigningChanges();
            Capabilities = new CapabilitiesChanges();
        }

        public bool IsDirty
        {
            get;
            set;
        }

        public bool Save(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                return false;
            }

            SavePath = savePath;
            return Save();
        }

        public bool Save()
        {
            if (string.IsNullOrEmpty(SavePath))
            {
                return false;
            }

            PList plist = new PList();
            plist.Root.Add(TYPE_KEY, TYPE_VALUE);
            plist.Root.Add(VERSION_KEY, VERSION);
            plist.Root.Add(BUILD_PLATFORM_KEY, Platform.ToString());
            plist.Root.Add(INFO_PLIST_KEY, InfoPlistChanges);
            plist.Root.Add(APP_CONFIG_KEY, AppConfig);
            plist.Root.Add(APP_CONFIG_ENABLED_KEY, AppConfigEnabled);
            plist.Root.Add(MANUAL_ENTITLEMENTS, ManualEntitlements);
            plist.Root.Add(FRAMEWORKS_KEY, Frameworks.Serialize());
            plist.Root.Add(FILES_AND_FOLDERS_KEY, FilesAndFolders.Serialize());
            plist.Root.Add(BUILD_SETTINGS_KEY, BuildSettings.Serialize());
            plist.Root.Add(SIGNING_KEY, Signing.Serialize());
            plist.Root.Add(SCRIPTS_KEY, Scripts.Serialize());
            
            
            plist.Root.Add(CAPABILITIES_KEY, Capabilities.Serialize());
            bool saved = plist.Save(SavePath, true);

            if (saved)
            {
                IsDirty = false;
            }

            return saved;
        }

        public static XcodeChangeFile Load(string pathToFile)
        {
            var changeFile = new XcodeChangeFile();

            if (changeFile.LoadFile(pathToFile))
            {
                return changeFile;
            }

            return null;
        }

        bool LoadFile(string pathToFile)
        {
            if (!File.Exists(pathToFile))
            {
                Debug.LogError("EgoXproject: Change file does not exist: " + pathToFile);
                return false;
            }

            SavePath = pathToFile;
            PList p = new PList();

            if (!p.Load(SavePath))
            {
                return false;
            }

            if (!Validate(p))
            {
                return false;
            }

            //set the platform. if non specified will default to ios
            BuildPlatform platform;

            if (p.Root.EnumValue (BUILD_PLATFORM_KEY, out platform))
            {
                Platform = platform;
            }
            else
            {
                Platform = BuildPlatform.iOS;
            }

            //reset everything
            Frameworks.Clear();
            FilesAndFolders.Clear();
            BuildSettings.Clear();
            Scripts.Clear();
            Signing.Clear();
            Capabilities.Clear();
            //load everything
            InfoPlistChanges = p.Root.DictionaryValue(INFO_PLIST_KEY).Copy() as PListDictionary;
            AppConfig = p.Root.DictionaryValue(APP_CONFIG_KEY) !=null ? p.Root.DictionaryValue(APP_CONFIG_KEY).Copy() as PListDictionary : new PListDictionary();
            AppConfigEnabled = p.Root.ArrayValue(APP_CONFIG_ENABLED_KEY) !=null ? p.Root.ArrayValue(APP_CONFIG_ENABLED_KEY).Copy() as PListArray : new PListArray();
            ManualEntitlements = p.Root.DictionaryValue(MANUAL_ENTITLEMENTS) !=null ?  p.Root.DictionaryValue(MANUAL_ENTITLEMENTS).Copy() as PListDictionary : new PListDictionary();
            LoadFrameworks(p.Root.DictionaryValue(FRAMEWORKS_KEY));
            LoadFilesAndFolders(p.Root.DictionaryValue(FILES_AND_FOLDERS_KEY)); 
            LoadScripts(p.Root.ArrayValue(SCRIPTS_KEY));
            LoadBuildSettings(p.Root.ArrayValue(BUILD_SETTINGS_KEY));
            LoadSigning(p.Root.DictionaryValue(SIGNING_KEY));
            LoadCapabilities(p.Root.DictionaryValue(CAPABILITIES_KEY));


            IsDirty = false;
            return true;
        }

        void LoadFrameworks(PListDictionary dic)
        {
            if (dic == null)
            {
                return;
            }

            try
            {
                Frameworks = new FrameworkChanges(dic);
            }
            catch
            {
                Debug.LogWarning("EgoXproject: Corrupt frameworks section in " + SavePath);
            }
        }

        void LoadFilesAndFolders(PListDictionary dic)
        {
            if (dic == null)
            {
                return;
            }

            try
            {
                FilesAndFolders = new FilesAndFolderChanges(dic);
            }
            catch
            {
                Debug.LogWarning("EgoXproject: Corrupt files and folders section in " + SavePath);
            }
        }

        void LoadScripts(PListArray array)
        {
            if (array == null)
            {
                return;
            }

            try
            {
                Scripts = new ScriptChanges(array);
            }
            catch
            {
                Debug.LogWarning("EgoXproject: Corrupt scripts section in " + SavePath);
            }
        }

        void LoadBuildSettings(PListArray array)
        {
            if (array == null)
            {
                return;
            }

            try
            {
                BuildSettings = new BuildSettingsChanges(array);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("EgoXproject: Corrupt build settings section in " + SavePath + " : " + e.Message);
            }
        }

        void LoadSigning(PListDictionary dic)
        {
            if (dic == null)
            {
                return;
            }

            try
            {
                Signing = new SigningChanges(dic);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("EgoXproject: Corrupt signing section in " + SavePath + " : " + e.Message);
            }
        }

        void LoadCapabilities(PListDictionary dic)
        {
            if (dic == null)
            {
                return;
            }

            try
            {
                Capabilities = new CapabilitiesChanges(dic);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("EgoXproject: Corrupt capabilities section in " + SavePath + " : " + e.Message);
            }
        }

        bool Validate(PList plist)
        {
            var typeValue = plist.Root.StringValue(TYPE_KEY);

            if (string.IsNullOrEmpty(typeValue) || typeValue != TYPE_VALUE)
            {
                return false;
            }

            var version = plist.Root.IntValue(VERSION_KEY);

            if (version != VERSION && !_supportedVersions.Contains(version))
            {
                return false;
            }

            if (plist.Root.DictionaryValue(INFO_PLIST_KEY) == null)
            {
                return false;
            }

            if (plist.Root.DictionaryValue(FRAMEWORKS_KEY) == null)
            {
                return false;
            }

            if (plist.Root.DictionaryValue(FILES_AND_FOLDERS_KEY) == null)
            {
                return false;
            }

            if (plist.Root.ArrayValue(BUILD_SETTINGS_KEY) == null)
            {
                return false;
            }

            if (plist.Root.ArrayValue(SCRIPTS_KEY) == null)
            {
                return false;
            }

            //signing section is optional for now
            //capabilities section is optional for now;
            return true;
        }

        public void Merge(XcodeChangeFile other)
        {
            if (Platform != other.Platform)
            {
                Debug.LogError("Cannot merge change files. Platforms do not match");
                return;
            }

            MergePListEntries(AppConfig, other.AppConfig);
            MergePListArray(AppConfigEnabled, other.AppConfigEnabled);
            MergePListEntries(InfoPlistChanges, other.InfoPlistChanges);
            MergePListEntries(ManualEntitlements, other.ManualEntitlements);
            Frameworks.Merge(other.Frameworks);
            FilesAndFolders.Merge(other.FilesAndFolders);
            BuildSettings.Merge(other.BuildSettings);
            Scripts.Merge(other.Scripts);
            Signing.Merge(other.Signing);
            Capabilities.Merge(other.Capabilities);
        }

        //PList
        // other replaces this one if match.
        // exception is empty strings. these are skipped
        void MergePListEntries(PListDictionary main, PListDictionary other)
        {
            foreach (var kvp in other)
            {
                //don't overwrite string with empty strings
                var str = kvp.Value as PListString;

                if (str != null)
                {
                    if (main.ContainsKey(kvp.Key) && string.IsNullOrEmpty(str.Value))
                    {
                        continue;
                    }
                }

                main[kvp.Key] = kvp.Value.Copy();
            }
        }
        
        void MergePListArray(PListArray main, PListArray other)
        {
            foreach (var el in other)
            {
                //don't overwrite string with empty strings
                var str = el as PListString;

                if (str != null)
                {
                    if (main.Contains(el) && string.IsNullOrEmpty(str.Value))
                    {
                        continue;
                    }
                }

                main.Add(el);
            }
        }

        public bool HasChanges()
        {
            return (AppConfigEnabled.Count > 0 ||
                    InfoPlistChanges.Count > 0 ||
                    ManualEntitlements.Count > 0 ||
                    Frameworks.HasChanges() ||
                    FilesAndFolders.HasChanges() ||
                    BuildSettings.HasChanges() ||
                    Scripts.HasChanges() ||
                    Signing.HasChanges() ||
                    Capabilities.HasChanges());
        }
        public bool LanguageEnabled(string key) {
            if (AppConfig == null) {
                Debug.LogError("No app config!");
            }

            if (!AppConfig.ContainsKey(LANGUAGES_DICT_KEY)) return false;
            PListArray arr = AppConfig[LANGUAGES_DICT_KEY] as PListArray;
            if (arr == null || arr.Count == 0) return false;

            return arr.Contains(new PListString(key));
 
        }
        
        public void SetLanguageEnabled(string key, bool enable=true) {
            if (AppConfig == null) {
                Debug.LogError("No app config!");
            }
            
            PListArray arr = null;
            if (AppConfig.ContainsKey(LANGUAGES_DICT_KEY)) {
                arr = AppConfig[LANGUAGES_DICT_KEY] as PListArray;
            }
            
            if(arr==null) arr = new PListArray();
            PListString id = new PListString(key);
            if (!enable && arr!=null && arr.Contains(id)) arr.Remove(id);
            if (enable && !arr.Contains(id)) {
                arr.Add(id);
            }

            AppConfig[LANGUAGES_DICT_KEY] = arr;
        }
    }
    
    
}
