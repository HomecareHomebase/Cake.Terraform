namespace Cake.Issues.Terraform.Results
{
    public class TerraformValidationRangePoint
    {
        [Newtonsoft.Json.JsonProperty("line")]
        public int Line { get; set; }

        [Newtonsoft.Json.JsonProperty("column")]
        public int Column { get; set; }

        [Newtonsoft.Json.JsonProperty("byte")]
        public int Byte { get; set; }
    }
}