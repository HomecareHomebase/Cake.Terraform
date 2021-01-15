using Cake.Core.IO;

namespace Cake.Issues.Terraform
{
    public class TerraformIssuesSettings : IssueProviderSettings
    {
        public TerraformIssuesSettings(FilePath logFilePath) : base(logFilePath)
        {
        }
    }
}