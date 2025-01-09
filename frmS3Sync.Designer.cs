namespace AWSS3Sync
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
            this.btnUploadFile = new System.Windows.Forms.Button();
            this.btnUploadFolder = new System.Windows.Forms.Button();
            this.btnBrowseFolder = new System.Windows.Forms.Button();
            this.btnSyncFolder = new System.Windows.Forms.Button();
            this.btnListS3Files = new System.Windows.Forms.Button();
            this.btnDownloadFiles = new System.Windows.Forms.Button();
            this.btnDeleteFiles = new System.Windows.Forms.Button();
            this.lblSourceFileName = new System.Windows.Forms.Label();
            this.lstS3FilesBox = new System.Windows.Forms.ListBox();
            this.lstLocalFilesBox = new System.Windows.Forms.ListBox();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.chkImagePReview = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();
            // 
            // btnUploadFile
            // 
            this.btnUploadFile.Location = new System.Drawing.Point(475, 12);
            this.btnUploadFile.Margin = new System.Windows.Forms.Padding(4);
            this.btnUploadFile.Name = "btnUploadFile";
            this.btnUploadFile.Size = new System.Drawing.Size(130, 30);
            this.btnUploadFile.TabIndex = 0;
            this.btnUploadFile.Text = "&Browse && Upload";
            this.btnUploadFile.UseVisualStyleBackColor = true;
            this.btnUploadFile.Click += new System.EventHandler(this.btnUploadFile_Click);
            // 
            // btnUploadFolder
            // 
            this.btnUploadFolder.Location = new System.Drawing.Point(770, 12);
            this.btnUploadFolder.Margin = new System.Windows.Forms.Padding(4);
            this.btnUploadFolder.Name = "btnUploadFolder";
            this.btnUploadFolder.Size = new System.Drawing.Size(125, 30);
            this.btnUploadFolder.TabIndex = 1;
            this.btnUploadFolder.Text = "Folder &Upload";
            this.btnUploadFolder.UseVisualStyleBackColor = true;
            this.btnUploadFolder.Click += new System.EventHandler(this.btnUploadFolder_Click);
            // 
            // btnBrowseFolder
            // 
            this.btnBrowseFolder.Location = new System.Drawing.Point(640, 12);
            this.btnBrowseFolder.Margin = new System.Windows.Forms.Padding(4);
            this.btnBrowseFolder.Name = "btnBrowseFolder";
            this.btnBrowseFolder.Size = new System.Drawing.Size(125, 30);
            this.btnBrowseFolder.TabIndex = 2;
            this.btnBrowseFolder.Text = "Browse &Folder";
            this.btnBrowseFolder.UseVisualStyleBackColor = true;
            this.btnBrowseFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);
            // 
            // btnSyncFolder
            // 
            this.btnSyncFolder.Location = new System.Drawing.Point(900, 12);
            this.btnSyncFolder.Margin = new System.Windows.Forms.Padding(4);
            this.btnSyncFolder.Name = "btnSyncFolder";
            this.btnSyncFolder.Size = new System.Drawing.Size(125, 30);
            this.btnSyncFolder.TabIndex = 3;
            this.btnSyncFolder.Text = "&Sync && Upload";
            this.btnSyncFolder.UseVisualStyleBackColor = true;
            this.btnSyncFolder.Click += new System.EventHandler(this.btnSyncFiles_Click);
            // 
            // btnListS3Files
            // 
            this.btnListS3Files.Location = new System.Drawing.Point(1265, 730);
            this.btnListS3Files.Margin = new System.Windows.Forms.Padding(4);
            this.btnListS3Files.Name = "btnListS3Files";
            this.btnListS3Files.Size = new System.Drawing.Size(125, 30);
            this.btnListS3Files.TabIndex = 4;
            this.btnListS3Files.Text = "&List Files";
            this.btnListS3Files.UseVisualStyleBackColor = true;
            this.btnListS3Files.Click += new System.EventHandler(this.btnListS3Files_Click);
            // 
            // btnDownloadFiles
            // 
            this.btnDownloadFiles.Enabled = false;
            this.btnDownloadFiles.Location = new System.Drawing.Point(1465, 730);
            this.btnDownloadFiles.Margin = new System.Windows.Forms.Padding(4);
            this.btnDownloadFiles.Name = "btnDownloadFiles";
            this.btnDownloadFiles.Size = new System.Drawing.Size(125, 30);
            this.btnDownloadFiles.TabIndex = 5;
            this.btnDownloadFiles.Text = "&Download";
            this.btnDownloadFiles.UseVisualStyleBackColor = true;
            this.btnDownloadFiles.Click += new System.EventHandler(this.btnDownloadFiles_Click);
            // 
            // btnDeleteFiles
            // 
            this.btnDeleteFiles.Location = new System.Drawing.Point(1365, 765);
            this.btnDeleteFiles.Margin = new System.Windows.Forms.Padding(4);
            this.btnDeleteFiles.Name = "btnDeleteFiles";
            this.btnDeleteFiles.Size = new System.Drawing.Size(125, 30);
            this.btnDeleteFiles.TabIndex = 11;
            this.btnDeleteFiles.Text = "&Delete Files";
            this.btnDeleteFiles.Enabled = false;
            this.btnDeleteFiles.UseVisualStyleBackColor = true;
            this.btnDeleteFiles.Click += new System.EventHandler(this.btnMoveToBackup_Click);
            // 
            // lblSourceFileName
            // 
            this.lblSourceFileName.AutoSize = true;
            this.lblSourceFileName.Location = new System.Drawing.Point(16, 12);
            this.lblSourceFileName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSourceFileName.Name = "lblSourceFileName";
            this.lblSourceFileName.Size = new System.Drawing.Size(19, 16);
            this.lblSourceFileName.TabIndex = 6;
            this.lblSourceFileName.Text = "---";
            // 
            // lstS3FilesBox
            // 
            this.lstS3FilesBox.FormattingEnabled = true;
            this.lstS3FilesBox.ItemHeight = 16;
            this.lstS3FilesBox.Location = new System.Drawing.Point(1032, 48);
            this.lstS3FilesBox.Margin = new System.Windows.Forms.Padding(4);
            this.lstS3FilesBox.Name = "lstS3FilesBox";
            this.lstS3FilesBox.Size = new System.Drawing.Size(724, 660);
            this.lstS3FilesBox.TabIndex = 7;
            this.lstS3FilesBox.SelectedIndexChanged += new System.EventHandler(this.lstFiles_SelectedIndexChanged);
            // 
            // lstLocalFilesBox
            // 
            this.lstLocalFilesBox.FormattingEnabled = true;
            this.lstLocalFilesBox.ItemHeight = 16;
            this.lstLocalFilesBox.Location = new System.Drawing.Point(250, 48);
            this.lstLocalFilesBox.Margin = new System.Windows.Forms.Padding(4);
            this.lstLocalFilesBox.Name = "lstLocalFilesBox";
            this.lstLocalFilesBox.Size = new System.Drawing.Size(774, 724);
            this.lstLocalFilesBox.TabIndex = 8;
            // 
            // picPreview
            // 
            this.picPreview.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(16, 48);
            this.picPreview.Margin = new System.Windows.Forms.Padding(4);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(226, 210);
            this.picPreview.TabIndex = 10;
            this.picPreview.TabStop = false;
            // 
            // chkImagePReview
            // 
            this.chkImagePReview.AutoSize = true;
            this.chkImagePReview.Checked = true;
            this.chkImagePReview.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkImagePReview.Location = new System.Drawing.Point(16, 266);
            this.chkImagePReview.Margin = new System.Windows.Forms.Padding(4);
            this.chkImagePReview.Name = "chkImagePReview";
            this.chkImagePReview.Size = new System.Drawing.Size(118, 20);
            this.chkImagePReview.TabIndex = 9;
            this.chkImagePReview.Text = "Image Preview";
            this.chkImagePReview.UseVisualStyleBackColor = true;
            // 
            // frmS3Sync
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1800, 850);
            this.Controls.Add(this.btnDeleteFiles);
            this.Controls.Add(this.btnBrowseFolder);
            this.Controls.Add(this.lstLocalFilesBox);
            this.Controls.Add(this.btnSyncFolder);
            this.Controls.Add(this.btnUploadFolder);
            this.Controls.Add(this.chkImagePReview);
            this.Controls.Add(this.btnListS3Files);
            this.Controls.Add(this.lstS3FilesBox);
            this.Controls.Add(this.lblSourceFileName);
            this.Controls.Add(this.btnDownloadFiles);
            this.Controls.Add(this.picPreview);
            this.Controls.Add(this.btnUploadFile);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "frmS3Sync";
            this.Text = "AWS S3 Files Sync";
            this.Load += new System.EventHandler(this.frmS3Access_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnUploadFile;
        private System.Windows.Forms.Button btnUploadFolder;
        private System.Windows.Forms.Button btnSyncFolder;
        private System.Windows.Forms.Button btnBrowseFolder;
        private System.Windows.Forms.Button btnListS3Files;
        private System.Windows.Forms.Button btnDownloadFiles;
        private System.Windows.Forms.Button btnDeleteFiles;
        private System.Windows.Forms.ListBox lstS3FilesBox;
        private System.Windows.Forms.ListBox lstLocalFilesBox;
        private System.Windows.Forms.Label lblSourceFileName;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.CheckBox chkImagePReview;
    }
}

