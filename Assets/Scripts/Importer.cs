using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LithFAQ;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;
using static DTX;
using System.IO;
using SFB;
using System;

public class Importer : MonoBehaviour
{
    [SerializeField]
    public DTXMaterial dtxMaterialList = new DTXMaterial();
    public Component DatReader;
    public GameObject RuntimeGizmoPrefab;


    [SerializeField]
    public Material defaultMaterial;
    public Color defaultColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    public UnityEngine.UI.Text infoBox;
    public UnityEngine.UI.Text loadingUI;
    public GameObject prefab;

    [Header("GameSpecific")]
    public string szProjectPath = String.Empty;
    public string szFileName;
    public uint nVersion;
    public int nSelectedGame;
    public Game eGame { get { return (Game)nSelectedGame; }}

    public Dictionary<ModelType, INIParser> configButes = new Dictionary<ModelType, INIParser>();

    public void OpenDAT()
    {
        ExtensionFilter[] efExtensionFiler = new[] {
                new ExtensionFilter("Lithtech World DAT", "dat" )
            };
        // Open file
        string[] aFilePaths = StandaloneFileBrowser.OpenFilePanel("Open File", "", efExtensionFiler, false);

        if (aFilePaths.Length > 0)
        {
            this.szProjectPath = Path.GetDirectoryName(aFilePaths[0]);
            szFileName = Path.GetFileName(aFilePaths[0]);
            ErrorLogger.LogInfo("Project Path: " + this.szProjectPath);
            ErrorLogger.LogInfo("File Name: " + szFileName);
            ErrorLogger.LogInfo("File Path: " + aFilePaths[0]);

            Array.Resize(ref aFilePaths, 2);
            string[] projectPath = StandaloneFileBrowser.OpenFolderPanel("Open Project Path", aFilePaths[0], false);
            if (projectPath.Length > 0)
            {
                aFilePaths[1] = projectPath[0];
                this.szProjectPath = projectPath[0];
            }
            else
            {
                ErrorLogger.LogWarning("User cancelled project path selection");
                return;
            }

            BinaryReader binaryReader = null;
            try
            {
                binaryReader = new BinaryReader(File.Open(aFilePaths[0], FileMode.Open));
            }
            catch (Exception ex)
            {
                ErrorLogger.LogFileError("Open", aFilePaths[0], ex);
                return;
            }

            if (binaryReader == null)
            {
                ErrorLogger.LogFileError("Open", aFilePaths[0], null);
                return;
            }

            try
            {
                nVersion = ReadDATVersion(ref binaryReader);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to read DAT version from {szFileName}", ex);
                return;
            }

            DatReader = null;

            //Build the string to find the correct DAT reader class based on the version read from the DAT
            string szComponentName = "LithFAQ.DATReader" + nVersion.ToString();
            
            try
            {
                DatReader = gameObject.AddComponent(Type.GetType(szComponentName));
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to add component: {szComponentName}", ex);
                DatReader = null;
            }

            if (DatReader == null)
            {
                ErrorLogger.LogLoadingError("DAT Reader", "Version " + nVersion.ToString(), $"Class {szComponentName} not found or incompatible");
                return;
            }

            //load the DAT
            try
            {
                IDATReader reader = (IDATReader)DatReader;
                reader.Load(binaryReader);
                ErrorLogger.LogInfo($"Successfully loaded DAT file version {nVersion}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogLoadingError("DAT", szFileName, "Failed to load data", ex);
                return;
            }

            UIActionManager.OnPostLoadLevel?.Invoke();

        }
        return;
    }

    public void OnEnable()
    {
        UIActionManager.OnPreLoadLevel += OnPreLoadLevel;
        UIActionManager.OnPreClearLevel += ClearLevel;

    }

    public void OnDisable()
    {
        UIActionManager.OnPreLoadLevel -= OnPreLoadLevel;
        UIActionManager.OnPreClearLevel -= ClearLevel;
    }

    private void OnPreLoadLevel()
    {
        ClearLevel();
        OpenDAT();
    }

    public void ClearLevel()
    {
        ResetAllProperties();
    }

    private void ResetAllProperties()
    {
        szProjectPath = String.Empty;
        szFileName = String.Empty;
        nVersion = 0;
        Resources.UnloadUnusedAssets();

        UIActionManager.OnReset?.Invoke();
    }

    private uint ReadDATVersion(ref BinaryReader binaryReader)
    {
        uint version = binaryReader.ReadUInt32();
        binaryReader.BaseStream.Position = 0; //reset back to start of the file so that our DAT reader can read it
        return version;
    }


    public ModelDefinition CreateModelDefinition(string szName, ModelType type, Dictionary<string, object> objectInfo = null)
    {
        //Bail out!
        if (type == ModelType.None)
            return null;

        ModelDefinition modelDefinition = new ModelDefinition();
        INIParser ini = new INIParser();

        if (objectInfo != null)
        {
            if (objectInfo.ContainsKey("MoveToFloor"))
            {
                modelDefinition.bMoveToFloor = (bool)objectInfo["MoveToFloor"];
            }
            if (objectInfo.ContainsKey("ForceNoMoveToGround"))
            {
                modelDefinition.bMoveToFloor = !(bool)objectInfo["ForceNoMoveToGround"];
            }
            if (objectInfo.ContainsKey("HumanOnly"))
            {
                modelDefinition.bMoveToFloor = true;
            }

        }

        if (type == ModelType.Character)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                string characterButesPath = szProjectPath + "\\Attributes\\CharacterButes.txt";
                if (File.Exists(characterButesPath))
                {
                    try
                    {
                        ini.Open(characterButesPath);
                        configButes.Add(type, ini); //stuff this away
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogFileError("Parse", characterButesPath, ex);
                        return null;
                    }
                }
                else
                {
                    ErrorLogger.LogFileError("Find", characterButesPath, null);
                    return null;
                }
            }


            Dictionary<string, string> item = null;

            if (type == ModelType.BodyProp)
            {
                if (objectInfo.ContainsKey("CharacterType"))
                {
                    szName = (string)objectInfo["CharacterType"];
                }
            }

            item = configButes[type].GetSectionsByName(szName);

            if (item == null)
            {
                ErrorLogger.LogWarning($"Could not find section '{szName}' in CharacterButes.txt");
                return null;
            }

            foreach (var key in item)
            {

                if (key.Key == "DefaultModel")
                {
                    modelDefinition.szModelFileName = key.Value.Replace("\"", "");
                }
                if (key.Key == "DefaultSkin0")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin1")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin2")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin3")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
            }

            modelDefinition.szModelFilePath = szProjectPath + "\\Models\\Characters\\" + modelDefinition.szModelFileName;
            modelDefinition.FitTextureList();

            return modelDefinition;
        }


        if (type == ModelType.Pickup)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                IDATReader reader = (IDATReader)DatReader;

                var nVersion = reader.GetVersion();

                string szButeFile = String.Empty;

                if(nVersion > 66)
                {
                    szButeFile = szProjectPath + "\\Attributes\\PickupButes.txt";
                }
                else
                {
                    szButeFile = szProjectPath + "\\Attributes\\Weapons.txt";
                }

                
                if (File.Exists(szButeFile))
                {
                    try
                    {
                        ini.Open(szButeFile);
                        configButes.Add(type, ini); //stuff this away
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogFileError("Parse", szButeFile, ex);
                        return null;
                    }
                }
                else
                {
                    ErrorLogger.LogFileError("Find", szButeFile, null);
                    return null;
                }
            }

            foreach (var sections in configButes[type].GetSections)
            {

                var test = sections.Value;

                // check if keys has a name
                if (sections.Value.ContainsKey("Name"))
                {
                    if (sections.Value["Name"].Replace("\"", "") != szName)
                    {
                        continue;
                    }
                    else
                    {

                        string modelName = String.Empty;

                        if (nVersion > 66)
                            configButes[type].ReadValue(sections.Key, "Model", "1x1square.abc");
                        else
                            configButes[type].ReadValue(sections.Key, "HHModel", "1x1square.abc");
                        
                        

                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = szProjectPath + "\\" + modelName.Replace("\"", "");
                        }

                        //get skins, could be up to 4, but not always defined.. FUN

                        if (nVersion > 66)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                string szSkinString = String.Format("Skin{0}", i);

                                modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, szSkinString, "").Replace("\"", String.Empty));

                            }
                            modelDefinition.FitTextureList();
                        }
                        else
                        {
                            modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, "HHSkin", "").Replace("\"", String.Empty));
                        }

                    }
                    return modelDefinition;
                }

            }

        }

        if (type == ModelType.WeaponItem)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                IDATReader reader = (IDATReader)DatReader;

                var nVersion = reader.GetVersion();

                string szButeFile = String.Empty;

                if (nVersion > 66)
                {
                    szButeFile = szProjectPath + "\\Attributes\\PickupButes.txt";
                }
                else
                {
                    szButeFile = szProjectPath + "\\Attributes\\Weapons.txt";
                }


                if (File.Exists(szButeFile))
                {
                    try
                    {
                        ini.Open(szButeFile);
                        configButes.Add(type, ini); //stuff this away
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogFileError("Parse", szButeFile, ex);
                        return null;
                    }
                }
                else
                {
                    ErrorLogger.LogFileError("Find", szButeFile, null);
                    return null;
                }
            }
            
            foreach (var sections in configButes[type].GetSections)
            {

                var test = sections.Value;

                // check if keys has a name
                if (sections.Value.ContainsKey("Name"))
                {
                    if (sections.Value["Name"].Replace("\"", "") != szName)
                    {
                        continue;
                    }
                    else
                    {

                        string modelName = String.Empty;

                        if (nVersion > 66)
                            configButes[type].ReadValue(sections.Key, "Model", "1x1square.abc");
                        else
                            configButes[type].ReadValue(sections.Key, "HHModel", "1x1square.abc");



                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = szProjectPath + "\\" + modelName.Replace("\"", "");
                        }

                        //get skins, could be up to 4, but not always defined.. FUN

                        if (nVersion > 66)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                string szSkinString = String.Format("Skin{0}", i);

                                modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, szSkinString, "").Replace("\"", String.Empty));

                            }
                            modelDefinition.FitTextureList();
                        }
                        else
                        {
                            modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, "HHSkin", "").Replace("\"", String.Empty));
                        }

                    }
                    return modelDefinition;
                }

            }

        }

        if (type == ModelType.Prop)
        {
            modelDefinition.modelType = type;

            //find the key "Filename" in the dictionary
            string szFilename = (string)objectInfo["Filename"];
            string szSkins = (string)objectInfo["Skin"];

            string[] szSkinArray = szSkins.Split(';');

            foreach (var szSkin in szSkinArray)
            {
                modelDefinition.szModelTextureName.Add(szSkin);
            }

            modelDefinition.szModelFileName = szFilename;
            modelDefinition.szModelFilePath = szProjectPath + "\\" + modelDefinition.szModelFileName;

            if (objectInfo.ContainsKey("Chromakey"))
            {
                modelDefinition.bChromakey = (bool)objectInfo["Chromakey"];
            }

            return modelDefinition;

        }

        if (type == ModelType.PropType)
        {
            modelDefinition.modelType = type;

            if (!configButes.ContainsKey(type))
            {
                string propTypesPath = szProjectPath + "\\Attributes\\PropTypes.txt";
                if (File.Exists(propTypesPath))
                {
                    try
                    {
                        ini.Open(propTypesPath);
                        configButes.Add(type, ini); //stuff this away
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogFileError("Parse", propTypesPath, ex);
                        return null;
                    }
                }
                else
                {
                    ErrorLogger.LogFileError("Find", propTypesPath, null);
                    return null;
                }
            }

            string szType = objectInfo["Type"].ToString();



            foreach (var sections in configButes[type].GetSections)
            {

                // check if keys has a name
                if (sections.Value.ContainsKey("Type"))
                {
                    if (sections.Value["Type"].Replace("\"", "") != szType)
                    {
                        continue;
                    }
                    else
                    {
                        string modelName = configButes[type].ReadValue(sections.Key, "Filename", "1x1square.abc");

                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = szProjectPath + "\\" + modelName.Replace("\"", "");
                        }

                        //get skins, could be up to 4, but not always defined.. FUN
                        string szSkins = configButes[type].ReadValue(sections.Key, "Skin", "");

                        string[] szSkinArray = szSkins.Split(';');

                        foreach (string szSkin in szSkinArray)
                        {
                            modelDefinition.szModelTextureName.Add(szSkin.Replace("\"", ""));
                        }
                    }

                    string szMoveToFloorString = configButes[type].ReadValue(sections.Key, "MoveToFloor", "0");

                    if (szMoveToFloorString == "1")
                    {
                        modelDefinition.bMoveToFloor = true;
                    }
                    else
                    {
                        modelDefinition.bMoveToFloor = false;
                    }

                    modelDefinition.bChromakey = configButes[type].ReadValue(sections.Key, "Chromakey", false);

                    return modelDefinition;
                }

            }

        }
        return null;
    }

    public void Quit()
    {
        Application.Quit();
    }
}
