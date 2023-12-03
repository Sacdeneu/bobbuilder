using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Unity.VisualScripting.Metadata;
using static UnityEditor.Progress;

public enum ItemType
{
    Folder,
    File,
    // Ajoutez d'autres types si nécessaire
}

public class FTPTreeViewItem : TreeViewItem
{
    // Ajoutez votre enum ici
    public ItemType Type { get; set; }
}


/// <summary>
/// Custom TreeView for displaying FTP entities (folders and files) in the Unity Editor.
/// </summary>
class FTPTreeView : TreeView
{
    private FTPTreeViewItem rootItem;
    private int currentId = 1; // Initialize with 1 to avoid using ID 0, which is reserved for the root.
    private int currentDepth = 1; // Initialize with 0 for the root.

    /// <summary>
    /// Constructor for FTPTreeView.
    /// </summary>
    /// <param name="treeViewState">The state of the tree view.</param>
    public FTPTreeView(TreeViewState treeViewState) : base(treeViewState)
    {
        Reload();
    }

    /// <summary>
    /// Builds the root of the tree view.
    /// </summary>
    /// <returns>The root TreeViewItem.</returns>
    protected override TreeViewItem BuildRoot()
    {
        rootItem = new FTPTreeViewItem { id = 0, depth = -1, displayName = "" };
        var FTProot = new FTPTreeViewItem { id = 1, depth = 0, displayName = ".", Type = ItemType.Folder };
        FTProot.icon = EditorGUIUtility.IconContent("d_Folder Icon").image as Texture2D;
        rootItem.AddChild(FTProot);
        SetExpanded(FTProot.id, true);
        SetupDepthsFromParentsAndChildren(rootItem);
        return rootItem;
    }

    /// <summary>
    /// Sets the root items for the tree view.
    /// </summary>
    /// <param name="rootItems">List of FTP entities to set as root items.</param>
    public void SetRootItems(List<FTPEntity> rootItems)
    {
        Reload();
        BuildTreeItems(rootItems, currentDepth);
    }

    /// <summary>
    /// Clears the tree view.
    /// </summary>
    public void ClearTree()
    {
        rootItem.children.Clear();
        Reload();
    }

    /// <summary>
    /// Generates a unique identifier for TreeView items.
    /// </summary>
    /// <returns>A unique identifier.</returns>
    private int GetUniqueId()
    {
        return currentId++;
    }

    /// <summary>
    /// Gets the TreeViewItem with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the TreeViewItem to retrieve.</param>
    /// <returns>The TreeViewItem with the specified ID, or null if not found.</returns>
    public FTPTreeViewItem GetItem(int id)
    {
        return GetItemRecursive(rootItem.children[0] as FTPTreeViewItem, id);
    }

    private FTPTreeViewItem GetItemRecursive(FTPTreeViewItem parentItem, int id)
    {
        if (parentItem == null)
            return null;

        if (parentItem.id == id)
            return parentItem;

        if (parentItem.children.Count > 0)
        {
            foreach (var child in parentItem.children)
            {
                FTPTreeViewItem result = GetItemRecursive(child as FTPTreeViewItem, id);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds tree items recursively based on the FTP entities.
    /// </summary>
    /// <param name="entities">List of FTP entities.</param>
    /// <param name="currentDepth">Current depth in the tree.</param>
    /// <param name="parentItem">Parent TreeViewItem.</param>
    private void BuildTreeItems(List<FTPEntity> entities, int currentDepth, FTPTreeViewItem parentItem = null)
    {
        if (entities == null || entities.Count == 0)
            return;

        foreach (var entity in entities)
        {
            FTPTreeViewItem item;

            if (entity is FTPFolder folder)
            {
                item = new FTPTreeViewItem { id = GetUniqueId(),  depth = currentDepth,  displayName = folder.Name, Type = ItemType.Folder };
                item.children = new();
                item.icon = EditorGUIUtility.IconContent("d_Folder Icon").image as Texture2D;
                BuildTreeItems(folder.Files, currentDepth + 1, item);
            }
            else if (entity is FTPFile file)
            {
                item = new FTPTreeViewItem{ id = GetUniqueId(), depth = currentDepth, displayName = file.Name, Type = ItemType.File };
                item.children = new();
                item.icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;
            }
            else
            {
                // Handle other FTP entity types if necessary
                continue;
            }

            if (parentItem != null)
            {
                parentItem.AddChild(item);
                SetExpanded(parentItem.id, true);
            }
            else
            {
                rootItem.children[0].AddChild(item);
            }
        }
    }
}