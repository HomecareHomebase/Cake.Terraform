using System;
using System.Collections.Generic;
using System.Text;

namespace Cake.Issues.Terraform.Results
{
    public class TerraformValidationResult
    {
        [Newtonsoft.Json.JsonProperty("valid")]
        public bool IsValid { get; set; }

        [Newtonsoft.Json.JsonProperty("error_count")]
        public int ErrorCount { get; set; }

        [Newtonsoft.Json.JsonProperty("warning_count")]
        public int WarningCount { get; set; }

        [Newtonsoft.Json.JsonProperty("diagnostics")]
        public IList<TerraformValidationDiagnostic> Diagnostics { get; set; }
    }
}
