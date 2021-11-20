using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GeoArcSysPaletteConverter.Utils
{
    public static class Dialogs
    {
        public static string OpenFileDialog(string Title, string Filter = "All files|*.*")
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = Title,
                Filter = Filter
            };
            if (openFileDialog.ShowDialog() == true)
                return openFileDialog.FileName;
            return null;
        }

        public static string OpenFolderDialog(string Title, string FolderName = null)
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = Title,
                IsFolderPicker = true,
                DefaultFileName = FolderName,

                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                EnsurePathExists = false,
                EnsureFileExists = false,
                Multiselect = false,
                ShowPlacesList = true
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok) return dlg.FileName;

            return null;
        }
    }
}