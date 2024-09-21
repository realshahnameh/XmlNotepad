using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SR = XmlNotepad.StringResources;


namespace XmlNotepad
{
    public partial class XsltViewer : UserControl
    {
        private ISite _site;
        private XmlCache _model;
        private bool _userSpecifiedOutput;
        private RecentFiles _xsltFiles;
        private RecentFilesComboBox _recentFilesCombo;
        private DelayedActions delayedActions = new DelayedActions();

        public XsltViewer()
        {
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            InitializeComponent();

            toolTip1.SetToolTip(this.BrowseButton, SR.BrowseButtonTooltip);
            toolTip1.SetToolTip(this.SourceFileName, SR.XslFileNameTooltip);
            toolTip1.SetToolTip(this.TransformButton, SR.TransformButtonTooltip);
            toolTip1.SetToolTip(this.OutputFileName, SR.XslOutputFileNameTooltip);

            BrowseButton.Click += new EventHandler(BrowseButton_Click);
            BrowseOutputButton.Click += new EventHandler(BrowseOutputButton_Click);
            this.SourceFileName.KeyDown += new KeyEventHandler(OnSourceFileNameKeyDown);
            this.OutputFileName.KeyDown += new KeyEventHandler(OnOutputFileNameKeyDown);

            this.xsltControl.DefaultStylesheetResource = "XmlNotepad.DefaultSS.xslt";

            TransformButton.SizeChanged += TransformButton_SizeChanged;

            xsltControl.LoadCompleted += OnXsltLoadCompleted;
        }

        public XsltControl GetXsltControl()
        {
            return this.xsltControl;
        }

        public void OnClosed()
        {
            this.xsltControl.OnClosed();
        }

        private void OnXsltLoadCompleted(object sender, PerformanceInfo info)
        {
            if (info != null)
            {
                if (Completed != null)
                {
                    Completed(this, info);
                }
                Debug.WriteLine("Browser loaded in {0} milliseconds", info.BrowserMilliseconds);
                info = null;
            }
        }

        public event EventHandler<PerformanceInfo> Completed;

        private void TransformButton_SizeChanged(object sender, EventArgs e)
        {
            CenterInputBoxes();
        }

        private void CenterInputBoxes()
        {
            // TextBoxes don't stretch when you set Anchor Top + Bottom, so we center the
            // Text Boxes manually so they look ok.
            int center = (tableLayoutPanel1.Height - SourceFileName.Height) / 2;
            SourceFileName.Margin = new Padding(0, center, 3, 3);
            OutputFileName.Margin = new Padding(0, center, 3, 3);
        }

        void OnSourceFileNameKeyDown(object sender, KeyEventArgs e)
        {
            this.OutputFileName.Text = ""; // need to recompute this then...
            if (e.KeyCode == Keys.Enter)
            {
                this.DisplayXsltResults();
            }
        }

        private void OnOutputFileNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                this.DisplayXsltResults();
            }
            else
            {
                _userSpecifiedOutput = true;
            }
        }

        private bool IsValidPath(string path)
        {
            try
            {
                Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
                if (uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile)
                {
                    string valid = System.IO.Path.GetFullPath(uri.LocalPath);
                }
                else
                {
                    string valid = System.IO.Path.GetFullPath(path);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        public async void DisplayXsltResults()
        {
            string xpath = this.SourceFileName.Text.Trim().Trim('"');
            if (!string.IsNullOrEmpty(xpath) && this._xsltFiles != null)
            {
                if (!IsValidPath(xpath))
                {
                    return;
                }
                Uri uri = this.xsltControl.ResolveRelativePath(xpath);
                if (uri != null)
                {
                    this._xsltFiles.AddRecentFile(uri);
                }
            }
            string output = this.OutputFileName.Text.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output))
            {
                _userSpecifiedOutput = false;
            }
            else if (!IsValidPath(output))
            {
                return;
            }
            bool hasDefaultXsltOutput = !string.IsNullOrEmpty(this._model.XsltDefaultOutput);
            if (!_userSpecifiedOutput && hasDefaultXsltOutput)
            {
                output = this._model.XsltDefaultOutput;
            }

            output = await this.xsltControl.DisplayXsltResults(this._model.Document, xpath, output, _userSpecifiedOutput, hasDefaultXsltOutput);
            if (!string.IsNullOrWhiteSpace(output))
            {
                this.OutputFileName.Text = MakeRelative(output);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.xsltControl.Top > 0 && this.Width > 0)
            {
                Graphics g = e.Graphics;
                Rectangle r = new Rectangle(0, 0, this.Width, this.xsltControl.Top);
                Color c1 = Color.FromArgb(250, 249, 245);
                Color c2 = Color.FromArgb(192, 192, 168);
                Color s1 = SystemColors.ControlLight;
                using (LinearGradientBrush brush = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, r);
                }
            }
            base.OnPaint(e);
        }

        public void SetSite(ISite site)
        {
            this._site = site;
            this.xsltControl.SetSite(site);
            IServiceProvider sp = (IServiceProvider)site;
            this._model = (XmlCache)site.GetService(typeof(XmlCache));
            this._model.ModelChanged -= new EventHandler<ModelChangedEventArgs>(OnModelChanged);
            this._model.ModelChanged += new EventHandler<ModelChangedEventArgs>(OnModelChanged);
        }

        void OnModelChanged(object sender, ModelChangedEventArgs e)
        {
            OnModelChanged(e);
        }

        void OnModelChanged(ModelChangedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_model.FileName))
                {
                    var uri = new Uri(_model.FileName);
                    if (uri != this.xsltControl.BaseUri)
                    {
                        this.xsltControl.BaseUri = uri;
                        this._xsltFiles.BaseUri = uri;
                        this.OutputFileName.Text = ""; // reset it since the file type might need to change...
                        _userSpecifiedOutput = false;
                    }
                }
                if (!string.IsNullOrEmpty(_model.XsltFileName))
                {
                    this.SourceFileName.Text = _model.XsltFileName;
                }
                if (e.ModelChangeType == ModelChangeType.Reloaded && this.Visible)
                {
                    this.delayedActions.StartDelayedAction("UpdateXslt", DisplayXsltResults, TimeSpan.FromSeconds(0.5));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("XsltViewer.OnModelChanged exception " + ex.Message);
            }
        }

        private string MakeRelative(string path)
        {
            if (path.StartsWith(System.IO.Path.GetTempPath()))
            {
                return path; // don't relativize temp dir.
            }
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                return path;
            }
            var relative = this.xsltControl.BaseUri.MakeRelativeUri(uri);
            if (relative.IsAbsoluteUri)
            {
                return relative.LocalPath;
            }
            string result = relative.GetComponents(UriComponents.SerializationInfoString, UriFormat.SafeUnescaped).Replace('/', '\\');
            if (result.Length > path.Length)
            {
                // keep the full path then, it's shorter!
                result = path;
            }
            return result;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = SR.XSLFileFilter;
                ofd.CheckPathExists = true;
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var rel = MakeRelative(ofd.FileName);
                    this.SourceFileName.Text = rel;
                }
            }
        }

        private void BrowseOutputButton_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog ofd = new SaveFileDialog())
            {
                ofd.Filter = this.xsltControl.GetOutputFileFilter(this.SourceFileName.Text.Trim());
                ofd.CheckPathExists = true;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var rel = MakeRelative(ofd.FileName);
                    this.OutputFileName.Text = rel;
                    _userSpecifiedOutput = true;
                }
            }
        }

        private void TransformButton_Click(object sender, EventArgs e)
        {
            this.DisplayXsltResults();
        }

        public void SetRecentFiles(RecentFiles recentXsltFiles)
        {
            _xsltFiles = recentXsltFiles;
            if (recentXsltFiles != null)
            {
                _recentFilesCombo = new RecentFilesComboBox(recentXsltFiles, this.SourceFileName);
                recentXsltFiles.RecentFileSelected += OnRecentFileSelected;
            }
        }

        private void OnRecentFileSelected(object sender, MostRecentlyUsedEventArgs args)
        {
            // do something?
        }
    }
}