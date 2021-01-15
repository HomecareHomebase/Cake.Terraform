namespace Cake.Issues.Terraform.Results
{
    public class TerraformValidationDiagnostic
    {
        [Newtonsoft.Json.JsonProperty("severity")]
        public string Severity { get; set; }

        [Newtonsoft.Json.JsonProperty("summary")]
        public string Summary { get; set; }

        [Newtonsoft.Json.JsonProperty("detail")]
        public string Detail { get; set; }

        [Newtonsoft.Json.JsonProperty("range")]
        public TerraformValidationDiagnosticRange Range { get; set; }
    }
}