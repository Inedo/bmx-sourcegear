using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.SourceGear
{
    [ProviderProperties("SourceGear Vault",
        "Supports Vault 3.0 and later; requires that the Vault Client (freely available from SourceGear.com) is installed.")]
    [CustomEditor(typeof(VaultProviderEditor))]
    public sealed class VaultProvider : SourceControlProviderBase, ILabelingProvider, IRevisionProvider, IClientCommandProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VaultProvider"/> class.
        /// </summary>
        public VaultProvider()
        {
        }

        /// <summary>
        /// Gets or sets the host name used by the vault client exe
        /// </summary>
        [Persistent]
        public string HostName { get; set; }
        /// <summary>
        /// Gets or sets the username used by the vault client exe
        /// </summary>
        [Persistent]
        public string Username { get; set; }
        /// <summary>
        /// Gets or sets the password used by the vault client exe
        /// </summary>
        [Persistent]
        public string Password { get; set; }
        /// <summary>
        /// Gets or sets the SSL indicator used by the vault client exe
        /// </summary>
        [Persistent]
        public bool UseSsl { get; set; }
        /// <summary>
        /// Gets or sets the user-defined path the the vault.exe client
        /// </summary>
        [Persistent]
        public string UserDefinedVaultClientExePath { get; set; }
        /// <summary>
        /// Gets or sets an indicator that, when true, will mask password
        /// for log files, etc
        /// </summary>
        [Persistent]
        public bool MaskPassword { get; set; }

        public override char DirectorySeparator
        {
            get { return VaultPath.DirectorySeparator; }
        }
        public bool SupportsCommandHelp
        {
            get { return true; }
        }

        public override string ToString()
        {
            return string.Format(
                "Vault on {0} (User Name: {1})",
                this.HostName,
                this.Username
            );
        }

        public override bool IsAvailable()
        {
            return !string.IsNullOrEmpty(this.FindVaultClientExePath());
        }
        public override void ValidateConnection()
        {
            this.RunCommand("LISTREPOSITORIES");
        }

        public override void GetLatest(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException("sourcePath");
            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentNullException("targetPath");

            var vaultSourcePath = new VaultPath(sourcePath);

            //Resolve target directory
            targetPath = targetPath.TrimEnd('\\'); // vault does not like paths that end with backslashes very much...
            var targetDir = new DirectoryInfo(targetPath);

            //Run command
            this.RunCommand(
                "GET",
                "\"" + vaultSourcePath.VaultFullPath + "\"",
                "-repository \"" + vaultSourcePath.RepositoryName + "\"",
                "-destpath \"" + targetDir.FullName + "\"",
                "-setfiletime modification",
                "-backup no"
            );
        }
        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var vaultSourcePath = new VaultPath(sourcePath);

            if (string.IsNullOrEmpty(vaultSourcePath.RepositoryName))
            {
                var xdoc = RunCommand("LISTREPOSITORIES");

                var repositories = new List<DirectoryEntryInfo>();
                foreach (XmlElement repositoryElement in xdoc.SelectNodes("/vault/listrepositories/repository"))
                {
                    var repositoryName =
                        repositoryElement.Attributes["name"] == null
                            ? repositoryElement.SelectSingleNode("name").InnerText
                            : repositoryElement.Attributes["name"].Value;
                    
                    repositories.Add(
                        new DirectoryEntryInfo(
                            repositoryName,
                            repositoryName + "$",
                            null,
                            null
                        )
                    );
                }

                return new DirectoryEntryInfo(string.Empty, string.Empty, repositories.ToArray(), null);
            }
            else
            {
                return this.GetDirectoryEntryInfoInRepository(vaultSourcePath);
            }
        }
        public override byte[] GetFileContents(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException("sourcePath");

            var foAgent = (IFileOperationsExecuter)this.Agent;

            var vaultSourcePath = new VaultPath(sourcePath);
            if (string.IsNullOrEmpty(vaultSourcePath.RepositoryName))
                throw new ArgumentException("No repository name specified");

            var targetPath = foAgent.CombinePath(Path.GetTempPath(), Guid.NewGuid().ToString("b"));
            foAgent.CreateDirectory(targetPath);
            try
            {
                this.RunCommand(
                    "GET",
                    "\"" + vaultSourcePath.VaultFullPath + "\"",
                    "-repository \"" + vaultSourcePath.RepositoryName + "\"",
                    "-destpath \"" + targetPath + "\"",
                    "-backup no"
                );

                var fileName = Path.GetFileName(sourcePath);
                return foAgent.ReadFileBytes(foAgent.CombinePath(targetPath, fileName));
            }
            finally
            {
                try { foAgent.DeleteDirectories(new[] { targetPath }); }
                catch { }
            }
        }

        public void ApplyLabel(string label, string sourcePath)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentNullException("label");
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException("sourcePath");

            var vaultSourcePath = new VaultPath(sourcePath);

            this.RunCommand(
                "LABEL",
                "\"" + vaultSourcePath.VaultFullPath + "\"",
                "\"" + label + "\"",
                "-repository \"" + vaultSourcePath.RepositoryName + "\""
            );
        }
        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentNullException("label");
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException("sourcePath");
            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentNullException("sourcePath");

            var vaultSourcePath = new VaultPath(sourcePath);

            targetPath = targetPath.TrimEnd('\\');

            this.RunCommand(
                "GETLABEL",
                "-destpath \"" + targetPath + "\"",
                "-setfiletime modification",
                "\"" + vaultSourcePath.VaultFullPath + "\"",
                "\"" + label + "\"",
                "-repository \"" + vaultSourcePath.RepositoryName + "\"",
                "-backup no"
            );
        }

        public byte[] GetCurrentRevision(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            var vaultSourcePath = new VaultPath(path);

            var doc = this.RunCommand(
                "VERSIONHISTORY",
                "\"" + vaultSourcePath.VaultFullPath + "\"",
                "-repository \"" + vaultSourcePath.RepositoryName + "\"",
                "-rowlimit 1"
            );

            var versionNode = doc.SelectSingleNode("/vault/history/item/@version");

            if (versionNode == null)
                throw new InvalidScPathException(path);

            return BitConverter.GetBytes(Int64.Parse(versionNode.Value));
        }

        private string FindVaultClientExePath()
        {
            if (!string.IsNullOrEmpty(this.UserDefinedVaultClientExePath))
                return this.UserDefinedVaultClientExePath;

            var result = this.ExecuteCommandLine("reg.exe", @"QUERY ""HKLM\SOFTWARE\SourceGear\Vault Client"" /v InstallDir", null);
            if (result.ExitCode != 0)
            {
                result = this.ExecuteCommandLine("reg.exe", @"QUERY ""HKLM\SOFTWARE\Wow6432Node\SourceGear\Vault Client"" /v InstallDir", null);
                if (result.ExitCode != 0)
                    return null;
            }

            foreach (var line in result.Output)
            {
                int index = line.IndexOf("REG_SZ");
                if (index >= 0)
                {
                    this.UserDefinedVaultClientExePath = Path.Combine(line.Substring(index + "REG_SZ".Length).Trim(), "vault.exe");
                    return this.UserDefinedVaultClientExePath;
                }
            }

            return null;
        }
        private XmlDocument RunCommand(string vaultCommand, params string[] parameters)
        {
            return this.RunCommand(vaultCommand, true, parameters);
        }
        private XmlDocument RunCommand(string vaultCommand, bool login, params string[] parameters)
        {
            string vaultExe = FindVaultClientExePath();
            if (string.IsNullOrEmpty(vaultExe))
                throw new NotAvailableException("Vault client not found.");

            var commandText = new StringBuilder();
            if (login)
                commandText.Append(this.GetVaultCommandLineArguments(false));

            commandText.Append(vaultCommand);

            foreach (var parameter in parameters)
            {
                commandText.Append(' ');
                commandText.Append(parameter);
            }

            var result = this.ExecuteCommandLine(vaultExe, commandText.ToString(), null);
            var cmdError = string.Join(Environment.NewLine, new List<string>(result.Error).ToArray());

            if (!string.IsNullOrEmpty(cmdError))
                throw new InvalidOperationException("Error executing vault: " + cmdError);

            var cmdResult = string.Join(Environment.NewLine, new List<string>(result.Output).ToArray());

            var xmlResult = new XmlDocument();
            try
            {
                xmlResult.LoadXml(cmdResult);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to load XML from Vault: " + cmdResult, ex);
            }

            // Validate Result
            string resultText;
            var successAttrib = xmlResult.SelectSingleNode("/vault/result/@success") as XmlAttribute;

            if (successAttrib != null)
            {
                resultText = successAttrib.Value;
            }
            else
            {
                //Vault 4.0 changed the success attribute into a node
                var node = xmlResult.SelectSingleNode("/vault/result/success");
                resultText = node.InnerText;
            }

            if (string.IsNullOrEmpty(resultText))
            {
                throw new InvalidOperationException("Unexpected XML returned from Vault: " + cmdResult);
            }
            else if (resultText.Equals("no", StringComparison.OrdinalIgnoreCase) || resultText.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                var errorText = string.Empty;
                var errorEl = xmlResult.SelectSingleNode("/vault/error") as XmlElement;
                if (errorEl != null)
                    errorText = errorEl.InnerText.Trim();

                throw new InvalidOperationException(errorText);
            }

            return xmlResult;
        }
        private DirectoryEntryInfo GetDirectoryEntryInfoInRepository(VaultPath path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path.RepositoryName));

            var doc = this.RunCommand("LISTFOLDER", "\"" + path.VaultFullPath + "\"", "-repository", "\"" + path.RepositoryName + "\"", "-norecursive");

            var folders = new List<string>();

            foreach (XmlElement folderElement in doc.SelectNodes("/vault/folder/folder"))
                folders.Add(folderElement.GetAttribute("name").Substring(path.VaultFullPath.Length).Trim('/'));

            var files = new List<string>();
            foreach (XmlElement fileElement in doc.SelectNodes("/vault/folder/file"))
                files.Add(fileElement.GetAttribute("name"));

            folders.Sort();
            files.Sort();

            var directoryEntries = new DirectoryEntryInfo[folders.Count];
            for(int i = 0; i < folders.Count; i++)
                directoryEntries[i] = new DirectoryEntryInfo(folders[i], path.ToString() + "/" + folders[i], null, null);

            var fileEntries = new FileEntryInfo[files.Count];
            for(int i = 0; i < files.Count; i++)
                fileEntries[i] = new FileEntryInfo(files[i], path.ToString() + "/" + files[i]);

            return new DirectoryEntryInfo(
                path.FolderName,
                path.ToString(),
                directoryEntries,
                fileEntries
            );
        }

        public void ExecuteClientCommand(string commandName, string arguments)
        {
            if (string.IsNullOrEmpty(commandName))
                throw new ArgumentNullException("commandName");

            var doc = this.RunCommand(commandName, arguments ?? string.Empty);

            var buffer = new StringBuilder();
            using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                doc.Save(writer);
            }

            foreach (var line in buffer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                this.LogInformation(line);
        }
        public IEnumerable<ClientCommand> GetAvailableCommands()
        {
            using (var stream = typeof(VaultProvider).Assembly.GetManifestResourceStream("Inedo.BuildMasterExtensions.SourceGear.VaultCommands.txt"))
            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine();
                while (line != null)
                {
                    var items = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    yield return new ClientCommand(items[0].Trim(), items[1].Trim());
                    line = reader.ReadLine();
                }
            }
        }
        public string GetClientCommandHelp(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                throw new ArgumentNullException("commandName");

            var doc = this.RunCommand("HELP", commandName);
            var usageElement = (XmlElement)doc.SelectSingleNode("/vault/usage");
            if (usageElement == null)
                return null;

            var text = usageElement.InnerText;

            int usageIndex = text.IndexOf("usage:", StringComparison.OrdinalIgnoreCase);
            if (usageIndex >= 0)
                text = text.Substring(usageIndex);

            return text;
        }
        public string GetClientCommandPreview()
        {
            return this.GetVaultCommandLineArguments(true);
        }

        private string GetVaultCommandLineArguments(bool hidePassword)
        {
            var commandText = new StringBuilder();
            commandText.Append("-host ");
            commandText.Append(this.HostName);
            commandText.Append(" -user \"");
            commandText.Append(this.Username);
            commandText.Append("\" -password \"");
            commandText.Append(hidePassword ? "XXXXX" : this.Password);
            commandText.Append("\" ");
            if (this.UseSsl)
                commandText.Append("-ssl ");

            return commandText.ToString();
        }
    }
}
