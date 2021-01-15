namespace Cake.Issues.Terraform.Results
{
    public class TerraformValidationDiagnosticRange
    {
        [Newtonsoft.Json.JsonProperty("filename")]
        public string Filename { get; set; }

        [Newtonsoft.Json.JsonProperty("start")]
        public TerraformValidationRangePoint Start { get; set; }

        [Newtonsoft.Json.JsonProperty("end")]
        public TerraformValidationRangePoint End { get; set; }
    }
}