namespace AWSS3Sync.UI
{
    partial class frmS3Sync
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnUploadFile = new Button();
            btnUploadFolder = new Button();
            btnBrowseFolder = new Button();
            btnSyncFolder = new Button();
            btnListS3Files = new Button();
            btnDownloadFiles = new Button();
            btnDeleteFiles = new Button();
            lblSourceFileName = new Label();
            lstS3FilesBox = new ListBox();
            lstLocalFilesBox = new ListBox();
            picPreview = new PictureBox();
            chkImagePReview = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)picPreview).BeginInit();
            SuspendLayout();
            // 
            // btnUploadFile
            // 
            btnUploadFile.Location = new Point(452, 15);
            btnUploadFile.Margin = new Padding(4, 5, 4, 5);
            btnUploadFile.Name = "btnUploadFile";
            btnUploadFile.Size = new Size(139, 38);
            btnUploadFile.TabIndex = 0;
            btnUploadFile.Text = "&Browse && Upload";
            btnUploadFile.UseVisualStyleBackColor = true;
            btnUploadFile.Click += btnUploadFile_Click;
            // 
            // btnUploadFolder
            // 
            btnUploadFolder.Enabled = false;
            btnUploadFolder.Location = new Point(770, 15);
            btnUploadFolder.Margin = new Padding(4, 5, 4, 5);
            btnUploadFolder.Name = "btnUploadFolder";
            btnUploadFolder.Size = new Size(125, 38);
            btnUploadFolder.TabIndex = 1;
            btnUploadFolder.Text = "Folder &Upload";
            btnUploadFolder.UseVisualStyleBackColor = true;
            btnUploadFolder.Click += btnUploadFolder_Click;
            // 
            // btnBrowseFolder
            // 
            btnBrowseFolder.Location = new Point(640, 15);
            btnBrowseFolder.Margin = new Padding(4, 5, 4, 5);
            btnBrowseFolder.Name = "btnBrowseFolder";
            btnBrowseFolder.Size = new Size(125, 38);
            btnBrowseFolder.TabIndex = 2;
            btnBrowseFolder.Text = "Browse &Folder";
            btnBrowseFolder.UseVisualStyleBackColor = true;
            btnBrowseFolder.Click += btnSelectFolder_Click;
            // 
            // btnSyncFolder
            // 
            btnSyncFolder.Enabled = false;
            btnSyncFolder.Location = new Point(900, 15);
            btnSyncFolder.Margin = new Padding(4, 5, 4, 5);
            btnSyncFolder.Name = "btnSyncFolder";
            btnSyncFolder.Size = new Size(125, 38);
            btnSyncFolder.TabIndex = 3;
            btnSyncFolder.Text = "&Sync && Upload";
            btnSyncFolder.UseVisualStyleBackColor = true;
            btnSyncFolder.Click += btnSyncFolder_Click;
            // 
            // btnListS3Files
            // 
            btnListS3Files.Location = new Point(1265, 912);
            btnListS3Files.Margin = new Padding(4, 5, 4, 5);
            btnListS3Files.Name = "btnListS3Files";
            btnListS3Files.Size = new Size(125, 38);
            btnListS3Files.TabIndex = 4;
            btnListS3Files.Text = "&List Files";
            btnListS3Files.UseVisualStyleBackColor = true;
            btnListS3Files.Click += btnListS3Files_Click;
            // 
            // btnDownloadFiles
            // 
            btnDownloadFiles.Enabled = false;
            btnDownloadFiles.Location = new Point(1465, 912);
            btnDownloadFiles.Margin = new Padding(4, 5, 4, 5);
            btnDownloadFiles.Name = "btnDownloadFiles";
            btnDownloadFiles.Size = new Size(125, 38);
            btnDownloadFiles.TabIndex = 5;
            btnDownloadFiles.Text = "&Download";
            btnDownloadFiles.UseVisualStyleBackColor = true;
            btnDownloadFiles.Click += btnDownloadFiles_Click;
            // 
            // btnDeleteFiles
            // 
            btnDeleteFiles.Enabled = false;
            btnDeleteFiles.Location = new Point(1365, 956);
            btnDeleteFiles.Margin = new Padding(4, 5, 4, 5);
            btnDeleteFiles.Name = "btnDeleteFiles";
            btnDeleteFiles.Size = new Size(125, 38);
            btnDeleteFiles.TabIndex = 11;
            btnDeleteFiles.Text = "&Delete Files";
            btnDeleteFiles.UseVisualStyleBackColor = true;
            btnDeleteFiles.Click += btnMoveToBackup_Click;
            // 
            // lblSourceFileName
            // 
            lblSourceFileName.AutoSize = true;
            lblSourceFileName.Location = new Point(16, 15);
            lblSourceFileName.Margin = new Padding(4, 0, 4, 0);
            lblSourceFileName.Name = "lblSourceFileName";
            lblSourceFileName.Size = new Size(27, 20);
            lblSourceFileName.TabIndex = 6;
            lblSourceFileName.Text = "---";
            // 
            // lstS3FilesBox
            // 
            lstS3FilesBox.FormattingEnabled = true;
            lstS3FilesBox.Location = new Point(1032, 60);
            lstS3FilesBox.Margin = new Padding(4, 5, 4, 5);
            lstS3FilesBox.Name = "lstS3FilesBox";
            lstS3FilesBox.Size = new Size(724, 824);
            lstS3FilesBox.TabIndex = 7;
            lstS3FilesBox.SelectedIndexChanged += lstFiles_SelectedIndexChanged;
            // 
            // lstLocalFilesBox
            // 
            lstLocalFilesBox.FormattingEnabled = true;
            lstLocalFilesBox.Location = new Point(250, 60);
            lstLocalFilesBox.Margin = new Padding(4, 5, 4, 5);
            lstLocalFilesBox.Name = "lstLocalFilesBox";
            lstLocalFilesBox.Size = new Size(774, 904);
            lstLocalFilesBox.TabIndex = 8;
            // 
            // picPreview
            // 
            picPreview.BackgroundImageLayout = ImageLayout.Stretch;
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.Location = new Point(16, 60);
            picPreview.Margin = new Padding(4, 5, 4, 5);
            picPreview.Name = "picPreview";
            picPreview.Size = new Size(226, 262);
            picPreview.TabIndex = 10;
            picPreview.TabStop = false;
            // 
            // chkImagePReview
            // 
            chkImagePReview.AutoSize = true;
            chkImagePReview.Checked = true;
            chkImagePReview.CheckState = CheckState.Checked;
            chkImagePReview.Location = new Point(16, 332);
            chkImagePReview.Margin = new Padding(4, 5, 4, 5);
            chkImagePReview.Name = "chkImagePReview";
            chkImagePReview.Size = new Size(128, 24);
            chkImagePReview.TabIndex = 9;
            chkImagePReview.Text = "Image Preview";
            chkImagePReview.UseVisualStyleBackColor = true;
            // 
            // frmS3Sync
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1800, 1062);
            Controls.Add(btnDeleteFiles);
            Controls.Add(btnBrowseFolder);
            Controls.Add(lstLocalFilesBox);
            Controls.Add(btnSyncFolder);
            Controls.Add(btnUploadFolder);
            Controls.Add(chkImagePReview);
            Controls.Add(btnListS3Files);
            Controls.Add(lstS3FilesBox);
            Controls.Add(lblSourceFileName);
            Controls.Add(btnDownloadFiles);
            Controls.Add(picPreview);
            Controls.Add(btnUploadFile);
            Margin = new Padding(4, 5, 4, 5);
            Name = "frmS3Sync";
            Text = "AWS S3 Files Sync";
            Load += frmS3Access_Load;
            ((System.ComponentModel.ISupportInitialize)picPreview).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button btnUploadFile;
        private System.Windows.Forms.Button btnUploadFolder;
        private System.Windows.Forms.Button btnSyncFolder;
        private System.Windows.Forms.Button btnBrowseFolder;
        private System.Windows.Forms.Button btnListS3Files;
        private System.Windows.Forms.Button btnDownloadFiles;
        private System.Windows.Forms.Button btnDeleteFiles;
        private System.Windows.Forms.Button btnMoveToBackup;
        private System.Windows.Forms.ListBox lstS3FilesBox;
        private System.Windows.Forms.ListBox lstLocalFilesBox;
        private System.Windows.Forms.Label lblSourceFileName;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.CheckBox chkImagePReview;
        private System.Windows.Forms.CheckBox chkGrantUserRoleAccess;
        private System.Windows.Forms.Button btnManageRoles; 
    }
}

