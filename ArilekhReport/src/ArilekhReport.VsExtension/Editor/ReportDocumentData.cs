using System;
using System.IO;
using System.Runtime.InteropServices;
using ArilekhReport.VsExtension;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace ArilekhReport.VsExtension.Editor
{
    /// <summary>
    /// Document data object for .rdx / .rds files.
    /// Holds the raw XML content and handles VS persistence (load/save/dirty tracking).
    /// The <see cref="ReportDesignerPane"/> reads/writes XML through this object.
    /// </summary>
    [ComVisible(true)]
    public sealed class ReportDocumentData :
        IVsPersistDocData,
        IVsFileChangeEvents,
        IPersist
    {
        private readonly FileType _fileType;
        private string  _filePath;
        private string  _content = string.Empty;
        private bool    _isDirty;

        // Subscribers (the pane) get notified when file content changes
        public event Action<string>? ContentChanged;

        public string Content
        {
            get => _content;
            set
            {
                if (_content == value) return;
                _content  = value;
                _isDirty  = true;
                ContentChanged?.Invoke(value);
            }
        }

        public bool IsDirty => _isDirty;

        public ReportDocumentData(string filePath, FileType fileType)
        {
            _filePath = filePath;
            _fileType = fileType;
            LoadFromDisk();
        }

        // ── File I/O ──────────────────────────────────────────────────

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_filePath))
                    _content = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
            }
            catch { /* file may not exist yet */ }
        }

        public void SaveToDisk(string path = null)
        {
            var target = path ?? _filePath;
            File.WriteAllText(target, _content, System.Text.Encoding.UTF8);
            _filePath = target;
            _isDirty  = false;
        }

        // ── IVsPersistDocData ─────────────────────────────────────────

        public int GetGuidEditorType(out Guid pClassID)
        {
            pClassID = _fileType == FileType.Rdx
                ? PkgGUID.RdxEditorFactory
                : PkgGUID.RdsEditorFactory;
            return VSConstants.S_OK;
        }

        public int IsDocDataDirty(out int pfDirty)
        {
            pfDirty = _isDirty ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int SetUntitledDocPath(string pszDocDataPath)
        {
            _filePath = pszDocDataPath;
            return VSConstants.S_OK;
        }

        public int LoadDocData(string pszMkDocument)
        {
            _filePath = pszMkDocument;
            LoadFromDisk();
            ContentChanged?.Invoke(_content);
            return VSConstants.S_OK;
        }

        public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
        {
            pbstrMkDocumentNew = _filePath;
            pfSaveCanceled     = 0;

            try
            {
                SaveToDisk();
                _isDirty = false;
                return VSConstants.S_OK;
            }
            catch
            {
                pfSaveCanceled = 1;
                return VSConstants.E_FAIL;
            }
        }

        public int Close()                         => VSConstants.S_OK;
        public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew) => VSConstants.S_OK;
        public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            _filePath = pszMkDocumentNew;
            return VSConstants.S_OK;
        }
        public int IsDocDataReloadable(out int pfReloadable) { pfReloadable = 1; return VSConstants.S_OK; }
        public int ReloadDocData(uint grfFlags)
        {
            LoadFromDisk();
            ContentChanged?.Invoke(_content);
            return VSConstants.S_OK;
        }

        // ── IVsFileChangeEvents ───────────────────────────────────────

        public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange) => VSConstants.S_OK;
        public int DirectoryChanged(string pszDirectory) => VSConstants.S_OK;

        // ── IPersist ──────────────────────────────────────────────────

        public int GetClassID(out Guid pClassID)
        {
            pClassID = PkgGUID.RdxEditorFactory;
            return VSConstants.S_OK;
        }
    }
}
