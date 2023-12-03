using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEditor.IMGUI.Controls;

public enum FTPSecurity
{
    ExplicitTLSIfAvailable,
    ExplicitTLS,
    ImplicitTLS,
    PlainFTP
}

public enum FTPAuthentication
{
    Anonymous,
    NormalUserPass,
    AskForPassword,
    Interactive,
    Account
}

public class FTPEntity
{
    public string Name { get; set; }
}

public class FTPFile : FTPEntity
{

}

public class FTPFolder : FTPEntity
{
    public List<FTPEntity> Files { get; set; }
}

public class BobBuilder : EditorWindow
{
    private VisualTreeAsset uiTree;
    private VisualElement root;
    private TextField serverAddressField;
    private IntegerField portField;
    private TextField usernameField;
    private TextField passwordField;
    private TextField remoteDirectoryField;
    private EnumField securityField;
    private Button connectButton;
    private Button buildAndUploadButton;
    private Button disconnectButton;
    private Label statusLabel;
    private IMGUIContainer treeViewContainer;
    private FTPTreeView fileTreeView;
    private TreeViewState treeViewState;


    private string serverAddress = "";
    private int port = 21; // Port FTP par défaut
    private string username = "";
    private string password = "";
    private string remoteDirectory = "";
    private FTPSecurity security = FTPSecurity.ExplicitTLSIfAvailable;
    private FTPAuthentication authentication = FTPAuthentication.NormalUserPass;
    private string account = "";
    private bool isConnected = false;


    [MenuItem("Build/Build and Upload")]
    public static void ShowWindow()
    {
        BobBuilder wnd = GetWindow<BobBuilder>();
        wnd.titleContent = new GUIContent("BobBuilder");
    }

    public void DisconnectFromFTP()
    {
        isConnected = false;
        fileTreeView.ClearTree();
        UpdateStatusLabel(root);
    }


    public void ConnectToFTP()
    {
        if (string.IsNullOrEmpty(serverAddress) || port <= 0 || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            EditorUtility.DisplayDialog("Missing Information", "Please fill in all the required FTP details.", "OK");
        }
        else
        {
            SaveSettings();
            PopulateTreeView();
            UpdateStatusLabel(root);
            isConnected = true;
        }
    }
    void LoadSavedSettings()
    {
        // Load the FTP settings from PlayerPrefs
        serverAddress = PlayerPrefs.GetString("ServerAddress", "");
        port = PlayerPrefs.GetInt("Port", 21);
        username = PlayerPrefs.GetString("Username", "");
        password = PlayerPrefs.GetString("Password", "");
        remoteDirectory = PlayerPrefs.GetString("RemoteDirectory", "");
        security = (FTPSecurity)PlayerPrefs.GetInt("FTPSecurity", (int)FTPSecurity.ExplicitTLSIfAvailable);
    }
    public void CreateGUI()
    {
        titleContent = new GUIContent("BobBuilder");
        LoadSavedSettings();
        root = rootVisualElement; // Get the root VisualElement


        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/BobBuilder/BobBuilder.uxml");
        var labelFromUXML = visualTree.Instantiate();
        root.Add(labelFromUXML);

        treeViewState = new TreeViewState();
        fileTreeView = new FTPTreeView(treeViewState);
        var treeContainer = root.Q<IMGUIContainer>("fileTreeContainer");
        treeContainer.onGUIHandler = () =>
        {
            // Draw the TreeView
            fileTreeView.OnGUI(treeContainer.contentContainer.contentRect);
        };

        // A stylesheet can be added to a VisualElement.
        // The style will be applied to the VisualElement and all of its children.
        //var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/BobBuilder.uss");
        //root.styleSheets.Add(styleSheet);
        connectButton = root.Q<Button>("connectButton");
        buildAndUploadButton = root.Q<Button>("buildAndUploadButton");
        disconnectButton = root.Q<Button>("disconnectButton");
        disconnectButton.style.display = isConnected ? DisplayStyle.Flex : DisplayStyle.None;
        serverAddressField = root.Q<TextField>("serverAddress");
        portField = root.Q<IntegerField>("port");
        usernameField = root.Q<TextField>("username");
        passwordField = root.Q<TextField>("password");
        remoteDirectoryField = root.Q<TextField>("remoteDirectory");
        securityField = root.Q<EnumField>("security");
        statusLabel = root.Q<Label>("statusLabel");
        /*fileListView = rootVisualElement.Q<ListView>("fileListView");
		fileListView.selectionType = SelectionType.Single;
		fileListView.showAddRemoveFooter = false;
		fileListView.itemsSource = ftpFileList;*/
        // Mettre à jour les champs d'entrée et les éléments d'UI avec les valeurs de votre classe BobBuilder
        serverAddressField.value = serverAddress;
        portField.value = port;
        usernameField.value = username;
        passwordField.value = password;
        remoteDirectoryField.value = remoteDirectory;
        securityField.SetValueWithoutNotify(security);

        BindUIEvents();

        UpdateStatusLabel(root);
    }

    void BindUIEvents()
    {
        connectButton.clicked += () =>
        {
            ConnectToFTP();
            connectButton.style.display = DisplayStyle.None;
            disconnectButton.style.display = DisplayStyle.Flex;
        };
        buildAndUploadButton.clicked += () => BuildAndUploadToFTP();
        disconnectButton.clicked += () =>
        {
            DisconnectFromFTP();
            disconnectButton.style.display = DisplayStyle.None;
            connectButton.style.display = DisplayStyle.Flex;
        };

        serverAddressField.RegisterValueChangedCallback(evt => serverAddress = evt.newValue);
        portField.RegisterValueChangedCallback(evt => port = evt.newValue);
        usernameField.RegisterValueChangedCallback(evt => username = evt.newValue);
        passwordField.RegisterValueChangedCallback(evt => password = evt.newValue);
        remoteDirectoryField.RegisterValueChangedCallback(evt => remoteDirectory = evt.newValue);
        securityField.RegisterValueChangedCallback(evt => security = (FTPSecurity)evt.newValue);
    }
    private void PopulateTreeView()
    {
        List<FTPEntity> ftpRootItems = GetFTPFileList(new List<FTPEntity>(), remoteDirectory);
        fileTreeView.SetRootItems(ftpRootItems);
    }

    private static string GetFtpUrl(string serverAddress, int port, string remoteDirectory, FTPSecurity security)
    {
        switch (security)
        {
            case FTPSecurity.ExplicitTLSIfAvailable:
                return $"ftp://{serverAddress}:{port}/{remoteDirectory}";
            case FTPSecurity.ExplicitTLS:
                return $"ftpes://{serverAddress}:{port}/{remoteDirectory}";
            case FTPSecurity.ImplicitTLS:
                return $"ftps://{serverAddress}:{port}/{remoteDirectory}";
            case FTPSecurity.PlainFTP:
                return $"ftp://{serverAddress}:{port}/{remoteDirectory}";
            default:
                throw new ArgumentOutOfRangeException(nameof(security), security, null);
        }
    }

    private static void SetSecurityOptions(FtpWebRequest request, FTPSecurity security)
    {
        switch (security)
        {
            case FTPSecurity.ExplicitTLSIfAvailable:
            case FTPSecurity.ExplicitTLS:
            case FTPSecurity.ImplicitTLS:
                request.EnableSsl = true;
                request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
                break;
            case FTPSecurity.PlainFTP:
                request.EnableSsl = false;
                break;
        }
    }

    

    void UpdateStatusLabel(VisualElement root)
    {
        var label = root.Q<Label>("statusLabel");
        if (isConnected)
        {
            label.text = "Connected to FTP";
        }
        else
        {
            label.text = "Not Connected";
        }
    }

    private string GetFolderPath(FTPTreeViewItem selectedItem)
    {
        if (selectedItem == null)
            return "";

        string path = selectedItem.displayName;

        // Traverse up the hierarchy to construct the full path
        while (selectedItem.parent != null)
        {
            path = Path.Combine(selectedItem.parent.displayName, path);
            selectedItem = selectedItem.parent as FTPTreeViewItem;
        }

        return path.Replace("\\", "/");
    }

    public void BuildAndUploadToFTP()
    {
        if (isConnected)
        {
            string outputDir = "Builds"; // Dossier de sortie pour la construction
            string scenePath = "Assets/Scenes/SampleScene.unity"; // Chemin de la scène que vous souhaitez construire

            // Personnalisez les paramètres de construction
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outputDir, "YourGame.exe"),
                target = BuildTarget.StandaloneWindows
            };

            var selectedItems = fileTreeView.GetSelection();

            if (selectedItems != null && selectedItems.Count == 1)
            {
                // Assuming only one item can be selected at a time
                var selectedItem = fileTreeView.GetItem(selectedItems[0]);

                if (selectedItem.Type == ItemType.File)
                {
                    // Display an error message when a file is selected
                    EditorUtility.DisplayDialog("Error", "Please select a folder to build.", "OK");
                }
                else if (selectedItem.Type == ItemType.Folder)
                {
                    // Get the path of the selected folder
                    string folderPath = GetFolderPath(selectedItem);

                    // Now you have the folder path, you can use it for building
                    // For demonstration purposes, let's print the folder path to the console
                    Debug.Log("Selected Folder Path: " + folderPath);

                    // Effectuez la construction
                    BuildPipeline.BuildPlayer(buildPlayerOptions);

                    UploadFtpDirectory(new DirectoryInfo(buildPlayerOptions.locationPathName).Parent.FullName, GetFtpUrl(serverAddress, port, folderPath, security), new NetworkCredential(username, password));

                    fileTreeView.ClearTree();
                    PopulateTreeView();

                    Debug.Log("Build and Upload completed!");
                }
            }
            else
            {
                // No item selected, display an error message
                EditorUtility.DisplayDialog("Error", "Please select a folder to build.", "OK");
            }
        }
    }

    void SaveSettings()
    {
        // Save the FTP settings using PlayerPrefs
        PlayerPrefs.SetString("ServerAddress", serverAddressField.value);
        PlayerPrefs.SetInt("Port", portField.value);
        PlayerPrefs.SetString("Username", usernameField.value);
        PlayerPrefs.SetString("Password", passwordField.value);
        PlayerPrefs.SetString("RemoteDirectory", remoteDirectoryField.value);
        PlayerPrefs.SetInt("FTPSecurity", Convert.ToInt32(securityField.value));
        PlayerPrefs.Save();
    }

    public static async Task<FtpStatusCode> FtpUploadAsync(string uri, string userName, string password, string filePath)
    {
        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
        request.Method = WebRequestMethods.Ftp.UploadFile;
        request.Credentials = new NetworkCredential(userName, password);
        // request.UsePassive is true by default.

        using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (Stream requestStream = request.GetRequestStream())
        {
            await fileStream.CopyToAsync(requestStream);
        }

        using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
        {
            return response.StatusCode;
        }
    }

    private List<FTPEntity> GetFTPFileList(List<FTPEntity> FTPArchitecture, string remoteDirectory)
    {
        FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + serverAddress + "/" + remoteDirectory + "/");
        request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
        request.Credentials = new NetworkCredential(username, password);

        List<string> lines = new List<string>();

        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
        using (Stream responseStream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(responseStream))
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                lines.Add(line);
            }
        }

        foreach (string line in lines)
        {
            string[] tokens = line.Split(new[] { ' ' }, 9, StringSplitOptions.RemoveEmptyEntries);
            string name = tokens[8];
            string permissions = tokens[0];

            if (permissions[0] == 'd')
            {
                // Ignore special entries "." and ".."
                if (!(name == "." || name.StartsWith("..")))
                {
                    FTPFolder folder = new FTPFolder { Name = name, Files = new List<FTPEntity>() };
                    folder.Files.AddRange(GetFTPFileList(new List<FTPEntity>(), $"{remoteDirectory}/{name}"));
                    FTPArchitecture.Add(folder);
                }
            }
            else
            {
                Console.WriteLine($"File {name}");
                FTPFile file = new FTPFile { Name = name };
                FTPArchitecture.Add(file);
            }
        }
        return FTPArchitecture;
    }

    void UploadFtpDirectory(
  string sourcePath, string url, NetworkCredential credentials)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(sourcePath);
        foreach (string file in files)
        {
            using (WebClient client = new WebClient())
            {
                Console.WriteLine($"Uploading {file}");
                client.Credentials = credentials;
                client.UploadFile(url + "/" + Path.GetFileName(file), file);
            }
        }

        IEnumerable<string> directories = Directory.EnumerateDirectories(sourcePath);
        foreach (string directory in directories)
        {
            string name = Path.GetFileName(directory);
            string directoryUrl = url + "/" + name;

            try
            {
                Console.WriteLine($"Creating {name}");
                FtpWebRequest requestDir =
                  (FtpWebRequest)WebRequest.Create(directoryUrl);
                requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
                requestDir.Credentials = credentials;
                requestDir.GetResponse().Close();
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode ==
                      FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    // probably exists already
                }
                else
                {
                    throw;
                }
            }

            UploadFtpDirectory(directory, directoryUrl + "/", credentials);
        }
    }


}