using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.SourceGear
{
    internal sealed class VaultProviderEditor : ProviderEditorBase 
    {
        private ValidatingTextBox txtUsername;
        private ValidatingTextBox txtPassword;
        private ValidatingTextBox txtVaultServer;
        private CheckBox chkUseSSL;
        private SourceControlFileFolderPicker txtOverrideExePath;

        public VaultProviderEditor()
        {
        }

        protected override void CreateChildControls()
        {
            this.txtUsername = new ValidatingTextBox { Width = 300, Required = true };
            this.txtPassword = new PasswordTextBox { Width = 270, Required = true };
            this.txtVaultServer = new ValidatingTextBox { Width = 300, Required = true };
            this.chkUseSSL = new CheckBox { Text = "Use SSL" };

            this.txtOverrideExePath = new SourceControlFileFolderPicker
            {
                DisplayMode = SourceControlBrowser.DisplayModes.FoldersAndFiles,
                ServerId = this.EditorContext.ServerId
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "Vault Connection",
                    "The following fields are used to connect to Vault's webservice. The values entered may be the same as what are entered in the Vault Windows client.",
                    false,
                    new StandardFormField("User Name:", this.txtUsername),
                    new StandardFormField("Password:", this.txtPassword),
                    new StandardFormField("Vault Server:", this.txtVaultServer),
                    new StandardFormField("", this.chkUseSSL)
                ),
                new FormFieldGroup("Vault.exe Path",
                    "You may manually specify the location of vault.exe here. If you leave this field blank, BuildMaster will attempt to determine the correct location autotmatically.",
                    false,
                    new StandardFormField("", this.txtOverrideExePath)
                )
            );
        }

        public override ProviderBase CreateFromForm()
        {
            this.EnsureChildControls();

            return new VaultProvider
            {
                Username = this.txtUsername.Text,
                Password = this.txtPassword.Text,
                HostName = this.txtVaultServer.Text,
                UseSsl = this.chkUseSSL.Checked,
                UserDefinedVaultClientExePath = this.txtOverrideExePath.Text
            };
        }

        public override void BindToForm(ProviderBase provider)
        {
            this.EnsureChildControls();

            var vaultProvider = (VaultProvider)provider;
            this.txtUsername.Text = vaultProvider.Username;
            this.txtPassword.Text = vaultProvider.Password;
            this.txtVaultServer.Text = vaultProvider.HostName;
            this.chkUseSSL.Checked = vaultProvider.UseSsl;
            this.txtOverrideExePath.Text = vaultProvider.UserDefinedVaultClientExePath;
        }
    }
}
