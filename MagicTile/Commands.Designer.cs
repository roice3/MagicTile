namespace MagicTile
{
	partial class Commands
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && (components != null) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Commands));
			this.m_text = new System.Windows.Forms.RichTextBox();
			this.SuspendLayout();
			// 
			// m_text
			// 
			this.m_text.Location = new System.Drawing.Point(12, 12);
			this.m_text.Name = "m_text";
			this.m_text.ReadOnly = true;
			this.m_text.Size = new System.Drawing.Size(405, 455);
			this.m_text.TabIndex = 0;
			this.m_text.Text = "";
			// 
			// Commands
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(429, 480);
			this.Controls.Add(this.m_text);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "Commands";
			this.Text = "Mouse and Keyboard Commands";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.RichTextBox m_text;
	}
}