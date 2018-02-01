using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.SourceGear
{
    internal sealed class VaultProviderEditor : ProviderEditorBase 
    {
        private ValidatingTextBox txtUsername;
        private ValidatingTextBox txtPassword;
        private ValidatingTextBox txtVaultServer;
        private CheckBox chkUseSSL;
        private FileBrowserTextBox txtOverrideExePath;

        protected override void CreateChildControls()
        {
            this.txtUsername = new ValidatingTextBox { Width = 300, Required = true };
            this.txtPassword = new PasswordTextBox { Width = 270, Required = true };
            this.txtVaultServer = new ValidatingTextBox { Width = 300, Required = true };
            this.chkUseSSL = new CheckBox { Text = "Use SSL" };

            this.txtOverrideExePath = new FileBrowserTextBox
            {
                IncludeFiles = true,
                ServerId = this.EditorContext.ServerId
            };

            this.Controls.Add(
                new SlimFormField("User name:", this.txtUsername),
                new SlimFormField("Password:", this.txtPassword),
                new SlimFormField("Vault server:", new Div(this.txtVaultServer), new Div(this.chkUseSSL)),
                new SlimFormField("Vault.exe path:", this.txtOverrideExePath)
            );
        }

        public override ProviderBase CreateFromForm()
        {
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
            var vaultProvider = (VaultProvider)provider;
            this.txtUsername.Text = vaultProvider.Username;
            this.txtPassword.Text = vaultProvider.Password;
            this.txtVaultServer.Text = vaultProvider.HostName;
            this.chkUseSSL.Checked = vaultProvider.UseSsl;
            this.txtOverrideExePath.Text = vaultProvider.UserDefinedVaultClientExePath;
        }
    }
}
