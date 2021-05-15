namespace NugetForUnity
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Handles the displaying, editing, and saving of the preferences for NuGet For Unity.
    /// </summary>
    public static class NugetPreferences
    {
        /// <summary>
        /// The current version of NuGet for Unity.
        /// </summary>
        public const string NuGetForUnityVersion = "3.0.2";

        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private static Vector2 scrollPosition;

        /// <summary>
        /// Draws the preferences GUI inside the Unity preferences window in the Editor.
        /// </summary>
        [PreferenceItem("NuGet For Unity")]
        public static void PreferencesGUI()
        {
            bool preferencesChangedThisFrame = false;

            EditorGUILayout.LabelField(string.Format("Version: {0}", NuGetForUnityVersion));

            if (NugetHelper.LocalNugetConfigFile == null)
            {
                NugetHelper.LoadNugetConfigFile();
            }

            bool installFromCache = EditorGUILayout.Toggle("Install From the Cache", NugetHelper.LocalNugetConfigFile.InstallFromCache);
            if (installFromCache != NugetHelper.LocalNugetConfigFile.InstallFromCache)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.LocalNugetConfigFile.InstallFromCache = installFromCache;
            }

            bool readOnlyPackageFiles = EditorGUILayout.Toggle("Read-Only Package Files", NugetHelper.LocalNugetConfigFile.ReadOnlyPackageFiles);
            if (readOnlyPackageFiles != NugetHelper.LocalNugetConfigFile.ReadOnlyPackageFiles)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.LocalNugetConfigFile.ReadOnlyPackageFiles = readOnlyPackageFiles;
            }

            bool verbose = EditorGUILayout.Toggle("Use Verbose Logging", NugetHelper.LocalNugetConfigFile.Verbose);
            if (verbose != NugetHelper.LocalNugetConfigFile.Verbose)
            {
                preferencesChangedThisFrame = true;
                NugetHelper.LocalNugetConfigFile.Verbose = verbose;
            }

            if (preferencesChangedThisFrame)
            {
                NugetHelper.LocalNugetConfigFile.Save(NugetHelper.NugetConfigFilePath);
            }

            EditorGUILayout.LabelField("Project Package Sources:");

            DisplayPackageSource(NugetHelper.LocalNugetConfigFile);

            EditorGUILayout.LabelField("Global Package Sources:");

            DisplayPackageSource(NugetHelper.GlobalNugetConfigFile);
        }

        private static void DisplayPackageSource(NugetConfigFile configFile)
        {
            if (configFile == null)
                return;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            bool preferencesChangedThisFrame = false;
            NugetPackageSource sourceToMoveUp = null;
            NugetPackageSource sourceToMoveDown = null;
            NugetPackageSource sourceToRemove = null;

            foreach (var source in configFile.PackageSources)
            {
                preferencesChangedThisFrame = DisplaySourceUI(source, out sourceToMoveUp, out sourceToMoveDown, out sourceToRemove);

                if (sourceToMoveUp != null)
                {
                    int index = configFile.PackageSources.IndexOf(sourceToMoveUp);
                    if (index > 0)
                    {
                        configFile.PackageSources[index] = configFile.PackageSources[index - 1];
                        configFile.PackageSources[index - 1] = sourceToMoveUp;
                    }
                    preferencesChangedThisFrame = true;
                }

                if (sourceToMoveDown != null)
                {
                    int index = configFile.PackageSources.IndexOf(sourceToMoveDown);
                    if (index < configFile.PackageSources.Count - 1)
                    {
                        configFile.PackageSources[index] = configFile.PackageSources[index + 1];
                        configFile.PackageSources[index + 1] = sourceToMoveDown;
                    }
                    preferencesChangedThisFrame = true;
                }

                if (sourceToRemove != null)
                {
                    configFile.PackageSources.Remove(sourceToRemove);
                    preferencesChangedThisFrame = true;
                }

                if (preferencesChangedThisFrame)
                {
                    configFile.Save(configFile.FilePath);
                }
            }

            if (GUILayout.Button(string.Format("Add New Source")))
            {
                configFile.PackageSources.Add(new NugetPackageSource("New Source", "source_path", 2));
                preferencesChangedThisFrame = true;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(string.Format("Reset To Default")))
            {
                NugetConfigFile.CreateDefaultFile(NugetHelper.NugetConfigFilePath);
                NugetHelper.LoadNugetConfigFile();
                preferencesChangedThisFrame = true;
            }
        }

        private static bool DisplaySourceUI(NugetPackageSource source, out NugetPackageSource sourceToMoveUp, out NugetPackageSource sourceToMoveDown, out NugetPackageSource sourceToRemove)
        {
            sourceToMoveUp = null;
            sourceToMoveDown = null;
            sourceToRemove = null;

            EditorGUILayout.BeginVertical();
            bool preferencesChangedThisFrame;
            {
                var last = source.ProtocolVersion;

                string[] options = new string[] { "0", "1", "2", "3" };
                source.ProtocolVersion = EditorGUILayout.Popup("Protocol Version", source.ProtocolVersion, options);

                if (source.ProtocolVersion < 2)
                {
                    Debug.LogWarning("Protocol Version 1 is obsolete");
                    source.ProtocolVersion = 2;
                }

                preferencesChangedThisFrame = last != source.ProtocolVersion;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.BeginVertical(GUILayout.Width(20));
                    {
                        GUILayout.Space(10);
                        bool isEnabled = EditorGUILayout.Toggle(source.IsEnabled, GUILayout.Width(20));
                        if (isEnabled != source.IsEnabled)
                        {
                            preferencesChangedThisFrame = true;
                            source.IsEnabled = isEnabled;
                        }
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    {
                        string name = EditorGUILayout.TextField(source.Name);
                        if (name != source.Name)
                        {
                            preferencesChangedThisFrame = true;
                            source.Name = name;
                        }

                        string savedPath = EditorGUILayout.TextField(source.SavedPath).Trim();
                        if (savedPath != source.SavedPath)
                        {
                            preferencesChangedThisFrame = true;
                            source.SavedPath = savedPath;
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Space(29);
                    EditorGUIUtility.labelWidth = 75;
                    EditorGUILayout.BeginVertical();

                    bool hasPassword = EditorGUILayout.Toggle("Credentials", source.HasPassword);
                    if (hasPassword != source.HasPassword)
                    {
                        preferencesChangedThisFrame = true;
                        source.HasPassword = hasPassword;
                    }

                    if (source.HasPassword)
                    {
                        string userName = EditorGUILayout.TextField("User Name", source.UserName);
                        if (userName != source.UserName)
                        {
                            preferencesChangedThisFrame = true;
                            source.UserName = userName;
                        }

                        string savedPassword = EditorGUILayout.PasswordField("Password", source.SavedPassword);
                        if (savedPassword != source.SavedPassword)
                        {
                            preferencesChangedThisFrame = true;
                            source.SavedPassword = savedPassword;
                        }
                    }
                    else
                    {
                        source.UserName = null;
                    }
                    EditorGUIUtility.labelWidth = 0;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(string.Format("Move Up")))
                    {
                        sourceToMoveUp = source;
                    }

                    if (GUILayout.Button(string.Format("Move Down")))
                    {
                        sourceToMoveDown = source;
                    }

                    if (GUILayout.Button(string.Format("Remove")))
                    {
                        sourceToRemove = source;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            return preferencesChangedThisFrame;
        }
    }
}
