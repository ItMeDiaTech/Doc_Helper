using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace DocHelper;

public class MainWindowViewModel : BindableBase
{
    private ObservableCollection<FileItem> _selectedFiles;
    private ObservableCollection<HyperlinkItem> _hyperlinks;
    private string[] _lookupIds;

    public MainWindowViewModel()
    {
        _selectedFiles = new ObservableCollection<FileItem>();
        _hyperlinks = new ObservableCollection<HyperlinkItem>();
        _lookupIds = Array.Empty<string>();
        SelectFilesCommand = new DelegateCommand(ExecuteSelectFiles);
        OpenFileCommand = new DelegateCommand<FileItem>(ExecuteOpenFile);
        SettingsCommand = new DelegateCommand(ExecuteSettings);
        LogsFolderCommand = new DelegateCommand(ExecuteLogsFolder);
    }

    public ObservableCollection<FileItem> SelectedFiles
    {
        get { return _selectedFiles; }
        set { SetProperty(ref _selectedFiles, value); }
    }

    public ObservableCollection<HyperlinkItem> Hyperlinks
    {
        get { return _hyperlinks; }
        set { SetProperty(ref _hyperlinks, value); }
    }

    public string[] LookupIds
    {
        get { return _lookupIds; }
        set { SetProperty(ref _lookupIds, value); }
    }

    public DelegateCommand SelectFilesCommand { get; }
    public DelegateCommand<FileItem> OpenFileCommand { get; }
    public DelegateCommand SettingsCommand { get; }
    public DelegateCommand LogsFolderCommand { get; }

    private void ExecuteSelectFiles()
    {
        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "All Files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedFiles.Clear();
            foreach (string filePath in openFileDialog.FileNames)
            {
                SelectedFiles.Add(new FileItem 
                { 
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath
                });
            }
        }
    }

    private void ExecuteOpenFile(FileItem? fileItem)
    {
        if (fileItem != null && File.Exists(fileItem.FilePath))
        {
            // Extract hyperlinks from the selected file
            ExtractHyperlinks(fileItem.FilePath);
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileItem.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Unable to open file: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void ExecuteSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.ShowDialog();
    }

    private void ExecuteLogsFolder()
    {
        InstallationService.OpenLogsFolder();
    }

    private void ExtractHyperlinks(string filePath)
    {
        Hyperlinks.Clear();
        
        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".docx":
                    ExtractHyperlinksFromWordDocument(filePath);
                    break;
                case ".txt":
                    ExtractHyperlinksFromTextFile(filePath);
                    break;
                default:
                    // For other file types, try to read as text
                    ExtractHyperlinksFromTextFile(filePath);
                    break;
            }

            // Collect all Document_IDs and Content_IDs into Lookup_ID array
            CollectLookupIds();
        }
        catch (Exception ex)
        {
            Hyperlinks.Add(new HyperlinkItem 
            { 
                DisplayText = "Error extracting hyperlinks", 
                Url = ex.Message 
            });
        }
    }

    private void ExtractHyperlinksFromWordDocument(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        
        if (body != null)
        {
            var hyperlinks = body.Descendants<Hyperlink>();
            
            foreach (var hyperlink in hyperlinks)
            {
                var displayText = hyperlink.InnerText;
                var relationshipId = hyperlink.Id?.Value ?? "";
                
                if (!string.IsNullOrEmpty(relationshipId))
                {
                    var relationship = document.MainDocumentPart?.HyperlinkRelationships
                        .FirstOrDefault(r => r.Id == relationshipId);
                    
                    if (relationship != null)
                    {
                        var url = relationship.Uri?.ToString() ?? "";
                        Hyperlinks.Add(new HyperlinkItem
                        {
                            DisplayText = displayText,
                            Url = url,
                            RelationshipId = relationshipId,
                            DocumentId = ExtractDocumentIdFromUrl(url),
                            ContentId = ExtractContentIdFromUrl(url),
                            Status = "",
                            JsonTitle = ""
                        });
                    }
                }
                else
                {
                    // Handle hyperlinks without relationship IDs
                    Hyperlinks.Add(new HyperlinkItem
                    {
                        DisplayText = displayText,
                        Url = "No URL found",
                        RelationshipId = "N/A",
                        DocumentId = "",
                        ContentId = "",
                        Status = "",
                        JsonTitle = ""
                    });
                }
            }
        }
    }

    private void ExtractHyperlinksFromTextFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var urlRegex = new Regex(@"https?://[^\s]+", RegexOptions.IgnoreCase);
        var matches = urlRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            var url = match.Value;
            Hyperlinks.Add(new HyperlinkItem
            {
                DisplayText = url,
                Url = url,
                RelationshipId = "N/A",
                DocumentId = ExtractDocumentIdFromUrl(url),
                ContentId = ExtractContentIdFromUrl(url),
                Status = "",
                JsonTitle = ""
            });
        }
    }

    private string ExtractDocumentIdFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        // Look for "?docid=" in the URL
        var docidIndex = url.IndexOf("?docid=", StringComparison.OrdinalIgnoreCase);
        if (docidIndex >= 0)
        {
            // Extract everything after "?docid="
            var startIndex = docidIndex + "?docid=".Length;
            var docid = url.Substring(startIndex);
            
            // If there are additional query parameters, only take up to the next &
            var ampersandIndex = docid.IndexOf('&');
            if (ampersandIndex >= 0)
            {
                docid = docid.Substring(0, ampersandIndex);
            }
            
            return docid;
        }

        return "";
    }

    private string ExtractContentIdFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        // Check if URL contains "TSRC" or "CMS"
        if (!url.Contains("TSRC", StringComparison.OrdinalIgnoreCase) && 
            !url.Contains("CMS", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        // Pattern: (CMS|TSRC)-[A-Za-z0-9\-]+-\d{6}
        var contentIdRegex = new Regex(@"(CMS|TSRC)-[A-Za-z0-9\-]+-\d{6}", RegexOptions.IgnoreCase);
        var match = contentIdRegex.Match(url);
        
        if (match.Success)
        {
            return match.Value;
        }

        return "";
    }

    private void CollectLookupIds()
    {
        var lookupIds = new List<string>();

        foreach (var hyperlink in Hyperlinks)
        {
            // Add Document_ID if not empty
            if (!string.IsNullOrEmpty(hyperlink.DocumentId))
            {
                lookupIds.Add(hyperlink.DocumentId);
            }

            // Add Content_ID if not empty
            if (!string.IsNullOrEmpty(hyperlink.ContentId))
            {
                lookupIds.Add(hyperlink.ContentId);
            }
        }

        // Remove duplicates and convert to array
        LookupIds = lookupIds.Distinct().ToArray();
    }
}