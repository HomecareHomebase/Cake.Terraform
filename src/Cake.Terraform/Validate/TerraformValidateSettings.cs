using Cake.Core.IO;

namespace Cake.Terraform.Validate
{
    public class TerraformValidateSettings : TerraformSettings
    {
        public FilePath OutputPath { get; set; }
        public bool NoColor { get; set; }
    }
}