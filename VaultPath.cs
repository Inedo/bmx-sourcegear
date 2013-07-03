
namespace Inedo.BuildMasterExtensions.SourceGear
{
    internal sealed class VaultPath
    {
        public const char DirectorySeparator = '/';

        public VaultPath(string path)
        {
            path = path ?? string.Empty;

            int index = path.IndexOf('$');
            if (index >= 0)
            {
                this.RepositoryName = path.Substring(0, index).Trim(DirectorySeparator);
                this.VaultFullPath = "$/" + path.Substring(index + 1).Trim(DirectorySeparator);
            }
            else
            {
                this.VaultFullPath = "$/" + path.Trim(DirectorySeparator);
            }
        }

        public string RepositoryName { get; private set; }
        public string VaultFullPath { get; private set; }
        public string FolderName
        {
            get { return this.VaultFullPath.Substring(this.VaultFullPath.LastIndexOf(DirectorySeparator) + 1); }
        }

        public override string ToString()
        {
            return this.RepositoryName + this.VaultFullPath;
        }
    }
}
