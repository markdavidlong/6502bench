﻿/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

using Asm65;
using CommonUtil;
using SourceGenWPF.ProjWin;
using System.Web.Script.Serialization;

namespace SourceGenWPF {
    /// <summary>
    /// This class manages user interaction.  The goal is for this to be relatively
    /// GUI-toolkit-agnostic, with all the WPF stuff tucked into the code-behind files.  An
    /// instance of this class is created by MainWindow when the app starts.
    /// 
    /// There is some Windows-specific stuff, like MessageBox and OpenFileDialog.
    /// </summary>
    public class MainController {
        #region Project state

        // Currently open project, or null if none.
        private DisasmProject mProject;

        // Pathname to 65xx data file.
        private string mDataPathName;

        // Pathname of .dis65 file.  This will be empty for a new project.
        private string mProjectPathName;

#if false
        /// <summary>
        /// Symbol subset, used to supply data to the symbol ListView.  Initialized with
        /// an empty symbol table.
        /// </summary>
        private SymbolTableSubset mSymbolSubset;

        /// <summary>
        /// Current code list view selection.  The length will match the DisplayList Count.
        ///
        /// WinForms was bad -- a simple foreach through SelectedIndices on a 500K-line data set
        /// took about 2.5 seconds on a fast Win10 x64 machine.  In WPF you get a list of
        /// selected objects, which is fine unless what you really want is the line number.
        /// </summary>
        private VirtualListViewSelection mCodeViewSelection = new VirtualListViewSelection();
#endif

        /// <summary>
        /// Data backing the code list.
        /// </summary>
        public LineListGen CodeListGen { get; private set; }

        #endregion Project state


        /// <summary>
        /// Reference back to MainWindow object.
        /// </summary>
        private MainWindow mMainWin;

        /// <summary>
        /// List of recently-opened projects.
        /// </summary>
        private List<string> mRecentProjectPaths = new List<string>(MAX_RECENT_PROJECTS);
        public const int MAX_RECENT_PROJECTS = 6;

        /// <summary>
        /// Activity log generated by the code and data analyzers.  Displayed in window.
        /// </summary>
        private DebugLog mGenerationLog;

        /// <summary>
        /// Timing data generated during analysis.
        /// </summary>
        TaskTimer mReanalysisTimer = new TaskTimer();

        /// <summary>
        /// Stack for navigate forward/backward.
        /// </summary>
        private NavStack mNavStack = new NavStack();

        /// <summary>
        /// Output format configuration.
        /// </summary>
        private Formatter.FormatConfig mFormatterConfig;

        /// <summary>
        /// Output format controller.
        /// 
        /// This is shared with the DisplayList.
        /// </summary>
        private Formatter mOutputFormatter;

        /// <summary>
        /// Pseudo-op names.
        /// 
        /// This is shared with the DisplayList.
        /// </summary>
        private PseudoOp.PseudoOpNames mPseudoOpNames;

        /// <summary>
        /// String we most recently searched for.
        /// </summary>
        private string mFindString = string.Empty;

        /// <summary>
        /// Initial start point of most recent search.
        /// </summary>
        private int mFindStartIndex = -1;

        /// <summary>
        /// Used to highlight the line that is the target of the selected line.
        /// </summary>
        private int mTargetHighlightIndex = -1;

        /// <summary>
        /// CPU definition used when the Formatter was created.  If the CPU choice or
        /// inclusion of undocumented opcodes changes, we need to wipe the formatter.
        /// </summary>
        private CpuDef mOutputFormatterCpuDef;

        /// <summary>
        /// Instruction description object.  Used for Info window.
        /// </summary>
        private OpDescription mOpDesc = OpDescription.GetOpDescription(null);

        /// <summary>
        /// If true, plugins will execute in the main application's AppDomain instead of
        /// the sandbox.
        /// </summary>
        private bool mUseMainAppDomainForPlugins = false;

        public MainController(MainWindow win) {
            mMainWin = win;
        }

        /// <summary>
        /// Perform one-time initialization after the Window has finished loading.  We defer
        /// to this point so we can report fatal errors directly to the user.
        /// </summary>
        public void WindowLoaded() {
            if (RuntimeDataAccess.GetDirectory() == null) {
                MessageBox.Show(Res.Strings.RUNTIME_DIR_NOT_FOUND,
                    Res.Strings.RUNTIME_DIR_NOT_FOUND_CAPTION,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
#if false
            try {
                PluginDllCache.PreparePluginDir();
            } catch (Exception ex) {
                string pluginPath = PluginDllCache.GetPluginDirPath();
                if (pluginPath == null) {
                    pluginPath = "<???>";
                }
                string msg = string.Format(Properties.Resources.PLUGIN_DIR_FAIL,
                    pluginPath + ": " + ex.Message);
                MessageBox.Show(this, msg, Properties.Resources.PLUGIN_DIR_FAIL_CAPTION,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
#endif

#if false
            logoPictureBox.ImageLocation = RuntimeDataAccess.GetPathName(LOGO_FILE_NAME);
            versionLabel.Text = string.Format(Properties.Resources.VERSION_FMT,
                Program.ProgramVersion);

            toolStripStatusLabel.Text = Properties.Resources.STATUS_READY;

            mProjectControl = this.codeListView;
            mNoProjectControl = this.noProjectPanel;

            // Clone the menu structure from the designer.  The same items are used for
            // both Edit > Actions and the right-click context menu in codeListView.
            mActionsMenuItems = new ToolStripItem[actionsToolStripMenuItem.DropDownItems.Count];
            for (int i = 0; i < actionsToolStripMenuItem.DropDownItems.Count; i++) {
                mActionsMenuItems[i] = actionsToolStripMenuItem.DropDownItems[i];
            }
#endif

#if false
            // Load the settings from the file.  Some things (like the symbol subset) need
            // these.  The general "apply settings" doesn't happen until a bit later, after
            // the sub-windows have been initialized.
            LoadAppSettings();

            // Init primary ListView (virtual, ownerdraw)
            InitCodeListView();

            // Init Symbols ListView (virtual, non-ownerdraw)
            mSymbolSubset = new SymbolTableSubset(new SymbolTable());
            symbolListView.SetDoubleBuffered(true);
            InitSymbolListView();

            // Init References ListView (non-virtual, non-ownerdraw)
            referencesListView.SetDoubleBuffered(true);

            // Place the main window and apply the various settings.
            SetAppWindowLocation();
#endif
            ApplyAppSettings();

#if false
            UpdateActionMenu();
            UpdateMenuItemsAndTitle();
            UpdateRecentLinks();

            ShowNoProject();
#endif

            ProcessCommandLine();
        }

        private void ProcessCommandLine() {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 2) {
                DoOpenFile(Path.GetFullPath(args[1]));
            }
        }

        /// <summary>
        /// Applies "actionable" settings to the ProjectView, pulling them out of the global
        /// settings object.  If a project is open, refreshes the display list and all sub-windows.
        /// </summary>
        private void ApplyAppSettings() {
            Debug.WriteLine("ApplyAppSettings...");
            AppSettings settings = AppSettings.Global;

            // Set up the formatter.
            mFormatterConfig = new Formatter.FormatConfig();
            AsmGen.GenCommon.ConfigureFormatterFromSettings(AppSettings.Global,
                ref mFormatterConfig);
            mFormatterConfig.mEndOfLineCommentDelimiter = ";";
            mFormatterConfig.mFullLineCommentDelimiterBase = ";";
            mFormatterConfig.mBoxLineCommentDelimiter = string.Empty;
            mFormatterConfig.mAllowHighAsciiCharConst = true;
            mOutputFormatter = new Formatter(mFormatterConfig);
            mOutputFormatterCpuDef = null;

            // Set pseudo-op names.  Entries aren't allowed to be blank, so we start with the
            // default values and merge in whatever the user has configured.
            mPseudoOpNames = PseudoOp.sDefaultPseudoOpNames.GetCopy();
            string pseudoCereal = settings.GetString(AppSettings.FMT_PSEUDO_OP_NAMES, null);
            if (!string.IsNullOrEmpty(pseudoCereal)) {
                PseudoOp.PseudoOpNames deser = PseudoOp.PseudoOpNames.Deserialize(pseudoCereal);
                if (deser != null) {
                    mPseudoOpNames.Merge(deser);
                }
            }

#if false
            // Configure the Symbols window.
            symbolUserCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_USER, false);
            symbolAutoCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_AUTO, false);
            symbolProjectCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_PROJECT, false);
            symbolPlatformCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_PLATFORM, false);
            symbolConstantCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_CONST, false);
            symbolAddressCheckBox.Checked =
                settings.GetBool(AppSettings.SYMWIN_SHOW_ADDR, false);

            // Set the code list view font.
            string fontStr = settings.GetString(AppSettings.CDLV_FONT, null);
            if (!string.IsNullOrEmpty(fontStr)) {
                FontConverter cvt = new FontConverter();
                try {
                    Font font = cvt.ConvertFromInvariantString(fontStr) as Font;
                    codeListView.Font = font;
                    Debug.WriteLine("Set font to " + font.ToString());
                } catch (Exception ex) {
                    Debug.WriteLine("Font convert failed: " + ex.Message);
                }
            }

            // Unpack the recent-project list.
            UnpackRecentProjectList();

            // Enable the DEBUG menu if configured.
            bool showDebugMenu = AppSettings.Global.GetBool(AppSettings.DEBUG_MENU_ENABLED, false);
            if (dEBUGToolStripMenuItem.Visible != showDebugMenu) {
                dEBUGToolStripMenuItem.Visible = showDebugMenu;
                mainMenuStrip.Refresh();
            }
#endif

            // Finally, update the display list generator with all the fancy settings.
            if (CodeListGen != null) {
                // Regenerate the display list with the latest formatter config and
                // pseudo-op definition.  (These are set as part of the refresh.)
                UndoableChange uc =
                    UndoableChange.CreateDummyChange(UndoableChange.ReanalysisScope.DisplayOnly);
                ApplyChanges(new ChangeSet(uc), false);
            }
        }

        /// <summary>
        /// Ensures that the named project is at the top of the list.  If it's elsewhere
        /// in the list, move it to the top.  Excess items are removed.
        /// </summary>
        /// <param name="projectPath"></param>
        private void UpdateRecentProjectList(string projectPath) {
            if (string.IsNullOrEmpty(projectPath)) {
                // This can happen if you create a new project, then close the window
                // without having saved it.
                return;
            }
            int index = mRecentProjectPaths.IndexOf(projectPath);
            if (index == 0) {
                // Already in the list, nothing changes.  No need to update anything else.
                return;
            }
            if (index > 0) {
                mRecentProjectPaths.RemoveAt(index);
            }
            mRecentProjectPaths.Insert(0, projectPath);

            // Trim the list to the max allowed.
            while (mRecentProjectPaths.Count > MAX_RECENT_PROJECTS) {
                Debug.WriteLine("Recent projects: dropping " +
                    mRecentProjectPaths[MAX_RECENT_PROJECTS]);
                mRecentProjectPaths.RemoveAt(MAX_RECENT_PROJECTS);
            }

            // Store updated list in app settings.  JSON-in-JSON is ugly and inefficient,
            // but it'll do for now.
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string cereal = ser.Serialize(mRecentProjectPaths);
            AppSettings.Global.SetString(AppSettings.PRVW_RECENT_PROJECT_LIST, cereal);

#if false
            UpdateRecentLinks();
#endif
        }


        public void OpenRecentProject(int projIndex) {
            if (!DoClose()) {
                return;
            }
            //DoOpenFile(mRecentProjectPaths[projIndex]);
            if (projIndex == 0) {
                DoOpenFile(@"C:\Src\6502bench\EXTRA\ZIPPY#ff2000.dis65");
            } else {
                DoOpenFile(@"C:\Src\6502bench\EXTRA\CRYLLAN.MISSION#b30100.dis65");
            }
        }

        /// <summary>
        /// Handles opening an existing project by letting the user select the project file.
        /// </summary>
        private void DoOpen() {
            if (!DoClose()) {
                return;
            }

            OpenFileDialog fileDlg = new OpenFileDialog() {
                Filter = ProjectFile.FILENAME_FILTER + "|" + Res.Strings.FILE_FILTER_ALL,
                FilterIndex = 1
            };
            if (fileDlg.ShowDialog() != true) {
                return;
            }

            string projPathName = Path.GetFullPath(fileDlg.FileName);
            DoOpenFile(projPathName);
        }

        /// <summary>
        /// Handles opening an existing project, given a pathname to the project file.
        /// </summary>
        private void DoOpenFile(string projPathName) {
            Debug.WriteLine("DoOpenFile: " + projPathName);
            Debug.Assert(mProject == null);

            if (!File.Exists(projPathName)) {
                string msg = string.Format(Res.Strings.ERR_FILE_NOT_FOUND_FMT, projPathName);
                MessageBox.Show(msg, Res.Strings.ERR_FILE_GENERIC_CAPTION,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DisasmProject newProject = new DisasmProject();
            newProject.UseMainAppDomainForPlugins = mUseMainAppDomainForPlugins;

            // Deserialize the project file.  I want to do this before loading the data file
            // in case we decide to store the data file name in the project (e.g. the data
            // file is a disk image or zip archive, and we need to know which part(s) to
            // extract).
            if (!ProjectFile.DeserializeFromFile(projPathName, newProject,
                    out FileLoadReport report)) {
                // Should probably use a less-busy dialog for something simple like
                // "permission denied", but the open file dialog handles most simple
                // stuff directly.
                ProjectLoadIssues dlg = new ProjectLoadIssues(report.Format(),
                    ProjectLoadIssues.Buttons.Cancel);
                dlg.ShowDialog();
                // ignore dlg.DialogResult
                return;
            }

            // Now open the data file, generating the pathname by stripping off the ".dis65"
            // extension.  If we can't find the file, show a message box and offer the option to
            // locate it manually, repeating the process until successful or canceled.
            const string UNKNOWN_FILE = "UNKNOWN";
            string dataPathName;
            if (projPathName.Length <= ProjectFile.FILENAME_EXT.Length) {
                dataPathName = UNKNOWN_FILE;
            } else {
                dataPathName = projPathName.Substring(0,
                    projPathName.Length - ProjectFile.FILENAME_EXT.Length);
            }
            byte[] fileData;
            while ((fileData = FindValidDataFile(ref dataPathName, newProject,
                    out bool cancel)) == null) {
                if (cancel) {
                    // give up
                    Debug.WriteLine("Abandoning attempt to open project");
                    return;
                }
            }

            // If there were warnings, notify the user and give the a chance to cancel.
            if (report.Count != 0) {
                ProjectLoadIssues dlg = new ProjectLoadIssues(report.Format(),
                    ProjectLoadIssues.Buttons.ContinueOrCancel);
                bool? ok = dlg.ShowDialog();

                if (ok != true) {
                    return;
                }
            }

            mProject = newProject;
            mProjectPathName = mProject.ProjectPathName = projPathName;
            mProject.SetFileData(fileData, Path.GetFileName(dataPathName));
            FinishPrep();
        }

        /// <summary>
        /// Finds and loads the specified data file.  The file's length and CRC must match
        /// the project's expectations.
        /// </summary>
        /// <param name="dataPathName">Full path to file.</param>
        /// <param name="proj">Project object.</param>
        /// <param name="cancel">Returns true if we want to cancel the attempt.</param>
        /// <returns></returns>
        private byte[] FindValidDataFile(ref string dataPathName, DisasmProject proj,
                out bool cancel) {
            FileInfo fi = new FileInfo(dataPathName);
            if (!fi.Exists) {
                Debug.WriteLine("File '" + dataPathName + "' doesn't exist");
                dataPathName = ChooseDataFile(dataPathName,
                    Res.Strings.OPEN_DATA_DOESNT_EXIST);
                cancel = (dataPathName == null);
                return null;
            }
            if (fi.Length != proj.FileDataLength) {
                Debug.WriteLine("File '" + dataPathName + "' has length=" + fi.Length +
                    ", expected " + proj.FileDataLength);
                dataPathName = ChooseDataFile(dataPathName,
                    string.Format(Res.Strings.OPEN_DATA_WRONG_LENGTH_FMT,
                        fi.Length, proj.FileDataLength));
                cancel = (dataPathName == null);
                return null;
            }
            byte[] fileData = null;
            try {
                fileData = LoadDataFile(dataPathName);
            } catch (Exception ex) {
                Debug.WriteLine("File '" + dataPathName + "' failed to load: " + ex.Message);
                dataPathName = ChooseDataFile(dataPathName,
                    string.Format(Res.Strings.OPEN_DATA_LOAD_FAILED_FMT, ex.Message));
                cancel = (dataPathName == null);
                return null;
            }
            uint crc = CRC32.OnWholeBuffer(0, fileData);
            if (crc != proj.FileDataCrc32) {
                Debug.WriteLine("File '" + dataPathName + "' has CRC32=" + crc +
                    ", expected " + proj.FileDataCrc32);
                // Format the CRC as signed decimal, so that interested parties can
                // easily replace the value in the .dis65 file.
                dataPathName = ChooseDataFile(dataPathName,
                    string.Format(Res.Strings.OPEN_DATA_WRONG_CRC_FMT,
                        (int)crc, (int)proj.FileDataCrc32));
                cancel = (dataPathName == null);
                return null;
            }

            cancel = false;
            return fileData;
        }

        /// <summary>
        /// Displays a "do you want to pick a different file" message, then (on OK) allows the
        /// user to select a file.
        /// </summary>
        /// <param name="origPath">Pathname of original file.</param>
        /// <param name="errorMsg">Message to display in the message box.</param>
        /// <returns>Full path of file to open.</returns>
        private string ChooseDataFile(string origPath, string errorMsg) {
            DataFileLoadIssue dlg = new DataFileLoadIssue(origPath, errorMsg);
            bool? ok = dlg.ShowDialog();
            if (ok != true) {
                return null;
            }

            OpenFileDialog fileDlg = new OpenFileDialog() {
                FileName = Path.GetFileName(origPath),
                Filter = Res.Strings.FILE_FILTER_ALL
            };
            if (fileDlg.ShowDialog() != true) {
                return null;
            }

            string newPath = Path.GetFullPath(fileDlg.FileName);
            Debug.WriteLine("User selected data file " + newPath);
            return newPath;
        }

        private bool DoSaveAs() {
            SaveFileDialog fileDlg = new SaveFileDialog() {
                Filter = ProjectFile.FILENAME_FILTER + "|" + Res.Strings.FILE_FILTER_ALL,
                FilterIndex = 1,
                ValidateNames = true,
                AddExtension = true,
                FileName = Path.GetFileName(mDataPathName) + ProjectFile.FILENAME_EXT
            };
            if (fileDlg.ShowDialog() == true) {
                string pathName = Path.GetFullPath(fileDlg.FileName);
                Debug.WriteLine("Project save path: " + pathName);
                if (DoSave(pathName)) {
                    // Success, record the path name.
                    mProjectPathName = mProject.ProjectPathName = pathName;

                    // add it to the title bar
#if false
                    UpdateMenuItemsAndTitle();
#endif
                    return true;
                }
            }
            return false;
        }

        // Save the project.  If it hasn't been saved before, use save-as behavior instead.
        private bool DoSave() {
            if (string.IsNullOrEmpty(mProjectPathName)) {
                return DoSaveAs();
            }
            return DoSave(mProjectPathName);
        }

        private bool DoSave(string pathName) {
            Debug.WriteLine("SAVING " + pathName);
            if (!ProjectFile.SerializeToFile(mProject, pathName, out string errorMessage)) {
                MessageBox.Show(Res.Strings.ERR_PROJECT_SAVE_FAIL + ": " + errorMessage,
                    Res.Strings.OPERATION_FAILED,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            mProject.ResetDirtyFlag();
#if false
            // If the debug dialog is visible, update it.
            if (mShowUndoRedoHistoryDialog != null) {
                mShowUndoRedoHistoryDialog.BodyText = mProject.DebugGetUndoRedoHistory();
            }
            UpdateMenuItemsAndTitle();
#endif

            // Update this, in case this was a new project.
            UpdateRecentProjectList(pathName);

#if false
            // Seems like a good time to save this off too.
            SaveAppSettings();
#endif

            return true;
        }

        /// <summary>
        /// Closes the project and associated modeless dialogs.  Unsaved changes will be
        /// lost, so if the project has outstanding changes the user will be given the
        /// opportunity to cancel.
        /// </summary>
        /// <returns>True if the project was closed, false if the user chose to cancel.</returns>
        private bool DoClose() {
            Debug.WriteLine("ProjectView.DoClose() - dirty=" +
                (mProject == null ? "N/A" : mProject.IsDirty.ToString()));
            if (mProject != null && mProject.IsDirty) {
                DiscardChanges dlg = new DiscardChanges();
                bool? ok = dlg.ShowDialog();
                if (ok != true) {
                    return false;
                } else if (dlg.UserChoice == DiscardChanges.Choice.SaveAndContinue) {
                    if (!DoSave()) {
                        return false;
                    }
                }
            }

#if false
            // Close modeless dialogs that depend on project.
            if (mShowUndoRedoHistoryDialog != null) {
                mShowUndoRedoHistoryDialog.Close();
            }
            if (mShowAnalysisTimersDialog != null) {
                mShowAnalysisTimersDialog.Close();
            }
            if (mShowAnalyzerOutputDialog != null) {
                mShowAnalyzerOutputDialog.Close();
            }
            if (mHexDumpDialog != null) {
                mHexDumpDialog.Close();
            }
#endif

            // Discard all project state.
            if (mProject != null) {
                mProject.Cleanup();
                mProject = null;
            }
            mDataPathName = null;
            mProjectPathName = null;
#if false
            mSymbolSubset = new SymbolTableSubset(new SymbolTable());
            mCodeViewSelection = new VirtualListViewSelection();
            mDisplayList = null;
            codeListView.VirtualListSize = 0;
            //codeListView.Items.Clear();
            ShowNoProject();
            InvalidateControls(null);
#endif
            mMainWin.ShowCodeListView = false;

            mGenerationLog = null;

            // Not necessary, but it lets us check the memory monitor to see if we got
            // rid of everything.
            GC.Collect();

            return true;
        }


        #region Project management

        private bool PrepareNewProject(string dataPathName, SystemDef sysDef) {
            DisasmProject proj = new DisasmProject();
            mDataPathName = dataPathName;
            mProjectPathName = string.Empty;
            byte[] fileData = null;
            try {
                fileData = LoadDataFile(dataPathName);
            } catch (Exception ex) {
                Debug.WriteLine("PrepareNewProject exception: " + ex);
                string message = Res.Strings.OPEN_DATA_FAIL_CAPTION;
                string caption = Res.Strings.OPEN_DATA_FAIL_MESSAGE + ": " + ex.Message;
                MessageBox.Show(caption, message, MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            proj.UseMainAppDomainForPlugins = mUseMainAppDomainForPlugins;
            proj.Initialize(fileData.Length);
            proj.PrepForNew(fileData, Path.GetFileName(dataPathName));

            proj.LongComments.Add(LineListGen.Line.HEADER_COMMENT_OFFSET,
                new MultiLineComment("6502bench SourceGen v" + App.ProgramVersion));

            // The system definition provides a set of defaults that can be overridden.
            // We pull everything of interest out and then discard the object.
            proj.ApplySystemDef(sysDef);

            mProject = proj;

            return true;
        }

        private void FinishPrep() {
            string messages = mProject.LoadExternalFiles();
            if (messages.Length != 0) {
                // ProjectLoadIssues isn't quite the right dialog, but it'll do.
                ProjectLoadIssues dlg = new ProjectLoadIssues(messages,
                    ProjectLoadIssues.Buttons.Continue);
                dlg.ShowDialog();
            }

            CodeListGen = new LineListGen(mProject, mMainWin.CodeDisplayList,
                mOutputFormatter, mPseudoOpNames);

            // Prep the symbol table subset object.  Replace the old one with a new one.
            //mSymbolSubset = new SymbolTableSubset(mProject.SymbolTable);

            RefreshProject(UndoableChange.ReanalysisScope.CodeAndData);
            //ShowProject();
            //InvalidateControls(null);
            mMainWin.ShowCodeListView = true;
            mNavStack.Clear();

            // Want to do this after ShowProject() or we see a weird glitch.
            UpdateRecentProjectList(mProjectPathName);
        }

        /// <summary>
        /// Loads the data file, reading it entirely into memory.
        /// 
        /// All errors are reported as exceptions.
        /// </summary>
        /// <param name="dataFileName">Full pathname.</param>
        /// <returns>Data file contents.</returns>
        private byte[] LoadDataFile(string dataFileName) {
            byte[] fileData;

            using (FileStream fs = File.Open(dataFileName, FileMode.Open, FileAccess.Read)) {
                // Check length; should have been caught earlier.
                if (fs.Length > DisasmProject.MAX_DATA_FILE_SIZE) {
                    throw new InvalidDataException(
                        string.Format(Res.Strings.OPEN_DATA_TOO_LARGE_FMT,
                            fs.Length / 1024, DisasmProject.MAX_DATA_FILE_SIZE / 1024));
                } else if (fs.Length == 0) {
                    throw new InvalidDataException(Res.Strings.OPEN_DATA_EMPTY);
                }
                fileData = new byte[fs.Length];
                int actual = fs.Read(fileData, 0, (int)fs.Length);
                if (actual != fs.Length) {
                    // Not expected -- should be able to read the entire file in one shot.
                    throw new Exception(Res.Strings.OPEN_DATA_PARTIAL_READ);
                }
            }

            return fileData;
        }

        /// <summary>
        /// Applies the changes to the project, adds them to the undo stack, and updates
        /// the display.
        /// </summary>
        /// <param name="cs">Set of changes to apply.</param>
        private void ApplyUndoableChanges(ChangeSet cs) {
            if (cs.Count == 0) {
                Debug.WriteLine("ApplyUndoableChanges: change set is empty");
            }
            ApplyChanges(cs, false);
            mProject.PushChangeSet(cs);
#if false
            UpdateMenuItemsAndTitle();

            // If the debug dialog is visible, update it.
            if (mShowUndoRedoHistoryDialog != null) {
                mShowUndoRedoHistoryDialog.BodyText = mProject.DebugGetUndoRedoHistory();
            }
#endif
        }

        /// <summary>
        /// Applies the changes to the project, and updates the display.
        /// 
        /// This is called by the undo/redo commands.  Don't call this directly from the
        /// various UI-driven functions, as this does not add the change to the undo stack.
        /// </summary>
        /// <param name="cs">Set of changes to apply.</param>
        /// <param name="backward">If set, undo the changes instead.</param>
        private void ApplyChanges(ChangeSet cs, bool backward) {
            mReanalysisTimer.Clear();
            mReanalysisTimer.StartTask("ProjectView.ApplyChanges()");

            mReanalysisTimer.StartTask("Save selection");
#if false
            int topItem = codeListView.TopItem.Index;
#else
            int topItem = 0;
#endif
            int topOffset = CodeListGen[topItem].FileOffset;
            LineListGen.SavedSelection savedSel = LineListGen.SavedSelection.Generate(
                CodeListGen, null /*mCodeViewSelection*/, topOffset);
            //savedSel.DebugDump();
            mReanalysisTimer.EndTask("Save selection");

            mReanalysisTimer.StartTask("Apply changes");
            UndoableChange.ReanalysisScope needReanalysis = mProject.ApplyChanges(cs, backward,
                out RangeSet affectedOffsets);
            mReanalysisTimer.EndTask("Apply changes");

            string refreshTaskStr = "Refresh w/reanalysis=" + needReanalysis;
            mReanalysisTimer.StartTask(refreshTaskStr);
            if (needReanalysis != UndoableChange.ReanalysisScope.None) {
                Debug.WriteLine("Refreshing project (" + needReanalysis + ")");
                RefreshProject(needReanalysis);
            } else {
                Debug.WriteLine("Refreshing " + affectedOffsets.Count + " offsets");
                RefreshCodeListViewEntries(affectedOffsets);
                mProject.Validate();    // shouldn't matter w/o reanalysis, but do it anyway
            }
            mReanalysisTimer.EndTask(refreshTaskStr);

            VirtualListViewSelection newSel = savedSel.Restore(CodeListGen, out int topIndex);
            //newSel.DebugDump();

            // Refresh the various windows, and restore the selection.
            mReanalysisTimer.StartTask("Invalidate controls");
#if false
            InvalidateControls(newSel);
#endif
            mReanalysisTimer.EndTask("Invalidate controls");

            // This apparently has to be done after the EndUpdate, and inside try/catch.
            // See https://stackoverflow.com/questions/626315/ for notes.
            try {
                Debug.WriteLine("Setting TopItem to index=" + topIndex);
#if false
                codeListView.TopItem = codeListView.Items[topIndex];
#endif
            } catch (NullReferenceException) {
                Debug.WriteLine("Caught an NRE from TopItem");
            }

            mReanalysisTimer.EndTask("ProjectView.ApplyChanges()");

            //mReanalysisTimer.DumpTimes("ProjectView timers:", mGenerationLog);
#if false
            if (mShowAnalysisTimersDialog != null) {
                string timerStr = mReanalysisTimer.DumpToString("ProjectView timers:");
                mShowAnalysisTimersDialog.BodyText = timerStr;
            }
#endif

            // Lines may have moved around.  Update the selection highlight.  It's important
            // we do it here, and not down in DoRefreshProject(), because at that point the
            // ListView's selection index could be referencing a line off the end.
#if false
            UpdateSelectionHighlight();
#endif
        }

        /// <summary>
        /// Refreshes the project after something of substance has changed.  Some
        /// re-analysis will be done, followed by a complete rebuild of the DisplayList.
        /// </summary>
        /// <param name="reanalysisRequired">Indicates whether reanalysis is required, and
        ///   what level.</param>
        private void RefreshProject(UndoableChange.ReanalysisScope reanalysisRequired) {
            Debug.Assert(reanalysisRequired != UndoableChange.ReanalysisScope.None);

            // NOTE: my goal is to arrange things so that reanalysis (data-only, and ideally
            // code+data) takes less than 100ms.  With that response time there's no need for
            // background processing and progress bars.  Since we need to do data-only
            // reanalysis after many common operations, the program becomes unpleasant to
            // use if we miss this goal, and progress bars won't make it less so.

            // Changing the CPU type or whether undocumented instructions are supported
            // invalidates the Formatter's mnemonic cache.  We can change these values
            // through undo/redo, so we need to check it here.
            if (mOutputFormatterCpuDef != mProject.CpuDef) {    // reference equality is fine
                Debug.WriteLine("CpuDef has changed, resetting formatter (now " +
                    mProject.CpuDef + ")");
                mOutputFormatter = new Formatter(mFormatterConfig);
                CodeListGen.SetFormatter(mOutputFormatter);
                CodeListGen.SetPseudoOpNames(mPseudoOpNames);
                mOutputFormatterCpuDef = mProject.CpuDef;
            }

#if false
            if (mDisplayList.Count > 200000) {
                string prevStatus = toolStripStatusLabel.Text;

                // The Windows stuff can take 50-100ms, potentially longer than the actual
                // work, so don't bother unless the file is very large.
                try {
                    mReanalysisTimer.StartTask("Do Windows stuff");
                    Application.UseWaitCursor = true;
                    Cursor.Current = Cursors.WaitCursor;
                    toolStripStatusLabel.Text = Res.Strings.STATUS_RECALCULATING;
                    Refresh();      // redraw status label
                    mReanalysisTimer.EndTask("Do Windows stuff");

                    DoRefreshProject(reanalysisRequired);
                } finally {
                    Application.UseWaitCursor = false;
                    toolStripStatusLabel.Text = prevStatus;
                }
            } else {
#endif
                DoRefreshProject(reanalysisRequired);
#if false
        }
#endif

            if (FormatDescriptor.DebugCreateCount != 0) {
                Debug.WriteLine("FormatDescriptor total=" + FormatDescriptor.DebugCreateCount +
                    " prefab=" + FormatDescriptor.DebugPrefabCount + " (" +
                    (FormatDescriptor.DebugPrefabCount * 100) / FormatDescriptor.DebugCreateCount +
                    "%)");
            }
        }

        /// <summary>
        /// Updates all of the specified ListView entries.  This is called after minor changes,
        /// such as editing a comment or renaming a label, that can be handled by regenerating
        /// selected parts of the DisplayList.
        /// </summary>
        /// <param name="offsetSet"></param>
        private void RefreshCodeListViewEntries(RangeSet offsetSet) {
            IEnumerator<RangeSet.Range> iter = offsetSet.RangeListIterator;
            while (iter.MoveNext()) {
                RangeSet.Range range = iter.Current;
                CodeListGen.GenerateRange(range.Low, range.High);
            }
        }

        private void DoRefreshProject(UndoableChange.ReanalysisScope reanalysisRequired) {
            if (reanalysisRequired != UndoableChange.ReanalysisScope.DisplayOnly) {
                mGenerationLog = new CommonUtil.DebugLog();
                mGenerationLog.SetMinPriority(CommonUtil.DebugLog.Priority.Debug);
                mGenerationLog.SetShowRelTime(true);

                mReanalysisTimer.StartTask("Call DisasmProject.Analyze()");
                mProject.Analyze(reanalysisRequired, mGenerationLog, mReanalysisTimer);
                mReanalysisTimer.EndTask("Call DisasmProject.Analyze()");
            }

            if (mGenerationLog != null) {
                //mReanalysisTimer.StartTask("Save _log");
                //mGenerationLog.WriteToFile(@"C:\Src\WorkBench\SourceGen\TestData\_log.txt");
                //mReanalysisTimer.EndTask("Save _log");

#if false
                if (mShowAnalyzerOutputDialog != null) {
                    mShowAnalyzerOutputDialog.BodyText = mGenerationLog.WriteToString();
                }
#endif
            }

            mReanalysisTimer.StartTask("Generate DisplayList");
            CodeListGen.GenerateAll();
            mReanalysisTimer.EndTask("Generate DisplayList");
        }

        #endregion Project management
    }
}
