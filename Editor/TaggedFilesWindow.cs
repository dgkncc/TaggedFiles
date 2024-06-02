using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TaggedFiles.Editor
{
    public class TaggedFilesWindow : EditorWindow
    {
        #region Fields

        private List<string> taggedFiles = new List<string>();
        private List<string> customOrder = new List<string>();
        private Vector2 scrollPosition;
        private bool showPaths = false;
        private int sortIndex = 0;
        private int selectedIndex = -1;
        private int doubleClickedIndex = -1;

        private const float MinNameWidth = 100f;
        private const float RemoveButtonWidth = 60f;

        private Texture windowIcon;
        private float iconPadding = 10f;

        #endregion

        #region Window

        [MenuItem("Window/Custom Windows/Tagged Files", false, 0)]
        public static void ShowWindow()
        {
            GetWindow<TaggedFilesWindow>("Tagged Files");
        }

        private void LoadWindowIcon()
        {
            windowIcon = Resources.Load<Texture>("icon_taggedfiles");
            if (windowIcon != null)
            {
                Texture2D paddedIcon = new Texture2D(windowIcon.width + (int)(2 * iconPadding), windowIcon.height + (int)(2 * iconPadding));

                Color[] pixels = new Color[paddedIcon.width * paddedIcon.height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.clear;
                }
                paddedIcon.SetPixels(pixels);

                paddedIcon.SetPixels((int)iconPadding, (int)iconPadding, windowIcon.width, windowIcon.height, ((Texture2D)windowIcon).GetPixels());
                paddedIcon.Apply();

                windowIcon = paddedIcon;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            LoadTaggedFiles();
            LoadWindowIcon();
        }

        private void OnDisable()
        {
            SaveTaggedFiles();
        }

        private void OnGUI()
        {
            GUIContent content = new GUIContent("Tagged Files", windowIcon);
            titleContent = content;

            DrawToolbar();
            HandleDragAndDrop();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DisplayTaggedFiles();
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region GUI Drawing

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showPaths = GUILayout.Toggle(showPaths, "Show Paths", EditorStyles.toolbarButton);
            string[] sortOptions = { "Sort by Custom", "Sort by Name", "Sort by Type" };
            sortIndex = EditorGUILayout.Popup(sortIndex, sortOptions, EditorStyles.toolbarDropDown, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(" Clear All ", EditorStyles.toolbarButton))
            {
                ShowClearAllConfirmationDialog();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DisplayTaggedFiles()
        {
            SortTaggedFiles();
            for (int i = 0; i < taggedFiles.Count; i++)
            {
                string taggedFilesPath = AssetDatabase.GUIDToAssetPath(taggedFiles[i]);
                DisplayTaggedFileItem(taggedFilesPath, i);
            }
        }

        private void DisplayTaggedFileItem(string taggedFile, int index)
        {
            bool isSelected = index == selectedIndex;
            Rect itemRect = EditorGUILayout.BeginHorizontal();
            float itemWidth = EditorGUIUtility.currentViewWidth - RemoveButtonWidth;
            Rect itemContentRect = new Rect(itemRect.position, new Vector2(itemWidth, itemRect.height));

            HighlightSelectedItem(isSelected, itemContentRect);
            bool assetExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(taggedFile) != null;

            GUI.color = assetExists ? Color.white : Color.red;

            DisplayItemContent(taggedFile, itemWidth, assetExists);
            HandleItemClick(index, itemRect, assetExists, taggedFile);

            EditorGUILayout.EndHorizontal();
            GUI.color = Color.white; // Reset GUI color
        }

        private void HighlightSelectedItem(bool isSelected, Rect itemContentRect)
        {
            if (isSelected)
            {
                EditorGUI.DrawRect(itemContentRect, new Color(0.4f, 0.6f, 0.9f, 0.5f));
            }
        }

        private void DisplayItemContent(string taggedFile, float itemWidth, bool assetExists)
        {
            Texture icon = assetExists ? AssetDatabase.GetCachedIcon(taggedFile) : EditorGUIUtility.FindTexture("console.erroricon.sml");
            GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));
            string displayedName = showPaths ? taggedFile : System.IO.Path.GetFileName(taggedFile);
            float availableWidth = itemWidth - displayedName.Length - RemoveButtonWidth;
            string truncatedName = TruncateStringToFit(displayedName, availableWidth);
            EditorGUILayout.LabelField(truncatedName, GUILayout.MinWidth(MinNameWidth));

            string guid = AssetDatabase.AssetPathToGUID(taggedFile);

            if (GUILayout.Button("Remove", GUILayout.Width(RemoveButtonWidth)))
            {
                RemoveTaggedFile(guid);
            }
        }

        #endregion

        #region Sorting

        private void SortTaggedFiles()
        {
            switch (sortIndex)
            {
                case 1:
                    taggedFiles.Sort((a, b) =>
                    {
                        string nameA = System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(a));
                        string nameB = System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(b));
                        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case 2:
                    taggedFiles.Sort((a, b) =>
                    {
                        string pathA = AssetDatabase.GUIDToAssetPath(a);
                        string pathB = AssetDatabase.GUIDToAssetPath(b);
                        string typeA = GetAssetType(pathA);
                        string typeB = GetAssetType(pathB);
                        return string.Compare(typeA, typeB, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                default:
                    taggedFiles = taggedFiles.OrderBy(guid => customOrder.IndexOf(guid)).ToList();
                    break;
            }
        }

        private string GetAssetType(string assetPath)
        {
            UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return obj ? obj.GetType().ToString() : "None";
        }

        #endregion

        #region Event Handling

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                        if (!taggedFiles.Contains(assetGuid))
                        {
                            taggedFiles.Add(assetGuid);
                            customOrder.Add(assetGuid);
                        }
                    }
                    SaveTaggedFiles();
                    evt.Use();
                }
            }
        }

        private void HandleItemClick(int index, Rect itemRect, bool assetExists, string taggedFile)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && itemRect.Contains(currentEvent.mousePosition))
            {
                selectedIndex = index;
                if (currentEvent.clickCount == 2)
                {
                    doubleClickedIndex = index;
                    currentEvent.Use();
                }
                Repaint();
            }

            if (doubleClickedIndex == index && currentEvent.type != EventType.MouseDown)
            {
                if (assetExists)
                {
                    OpenAsset(taggedFile);
                }
                else
                {
                    Debug.LogError("The selected asset is missing!");
                }
                doubleClickedIndex = -1;
            }
        }

        private void OpenAsset(string assetPath)
        {
            UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj != null)
            {
                AssetDatabase.OpenAsset(obj);
            }
        }

        private void RemoveTaggedFile(string guid)
        {
            taggedFiles.Remove(guid);
            customOrder.Remove(guid);
            SaveTaggedFiles();
        }

        #endregion

        #region Helper Methods

        private void ShowClearAllConfirmationDialog()
        {
            bool clearAll = EditorUtility.DisplayDialog("Clear All Tagged Files", "Are you sure you want to clear all tagged files?", "Yes", "No");
            if (clearAll)
            {
                taggedFiles.Clear();
                customOrder.Clear();
                SaveTaggedFiles();
            }
        }

        private string TruncateStringToFit(string input, float width)
        {
            if (showPaths)
            {
                string[] parts = input.Split('/');
                string truncatedPath = string.Join("/", parts.Take(parts.Length - 1)) + "/";
                truncatedPath = TruncateTextToFit(truncatedPath, width - parts.Last().Length - 5);
                string truncatedName = parts.Last();

                if (truncatedPath.Length < input.Length)
                {
                    truncatedName = TruncateTextToFit(truncatedName, width);
                }
                return truncatedPath + truncatedName;
            }
            else
            {
                return TruncateTextToFit(input, width);
            }
        }

        private string TruncateTextToFit(string input, float width)
        {
            float textWidth = EditorStyles.label.CalcSize(new GUIContent(input)).x;

            if (textWidth <= width)
            {
                return input;
            }

            for (int i = input.Length; i > 0; i--)
            {
                string truncatedString = input.Substring(0, i) + "...";
                textWidth = EditorStyles.label.CalcSize(new GUIContent(truncatedString)).x;
                if (textWidth <= width)
                {
                    return truncatedString;
                }
            }

            return "...";
        }

        #endregion

        #region File Management

        private void LoadTaggedFiles()
        {
            string jsonData = EditorPrefs.GetString("TaggedFilesAndOrder", "{}");
            TaggedFileListWrapper data = JsonUtility.FromJson<TaggedFileListWrapper>(jsonData);
            if (data != null)
            {
                taggedFiles = data.taggedFiles ?? new List<string>();
                customOrder = data.customOrder ?? new List<string>(); ;
            }
            else
            {
                taggedFiles = new List<string>();
                customOrder = new List<string>();
            }
        }

        private void SaveTaggedFiles()
        {
            TaggedFileListWrapper data = new TaggedFileListWrapper(taggedFiles, customOrder);
            string jsonData = JsonUtility.ToJson(data);
            EditorPrefs.SetString("TaggedFilesAndOrder", jsonData);
        }

        #endregion
    }

    [Serializable]
    public class TaggedFileListWrapper
    {
        public List<string> taggedFiles;
        public List<string> customOrder;
        public TaggedFileListWrapper(List<string> TaggedFiles, List<string> CustomOrder)
        {
            this.taggedFiles = TaggedFiles;
            this.customOrder = CustomOrder;
        }
    }

}
