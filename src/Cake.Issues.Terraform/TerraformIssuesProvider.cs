using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cake.Core.Diagnostics;
using Cake.Issues.Terraform.Results;
using Newtonsoft.Json;

namespace Cake.Issues.Terraform
{
    public class TerraformIssuesProvider : BaseConfigurableIssueProvider<TerraformIssuesSettings>
    {
        public TerraformIssuesProvider(ICakeLog log, TerraformIssuesSettings issueProviderSettings) : base(log, issueProviderSettings)
        {
        }

        protected override IEnumerable<IIssue> InternalReadIssues()
        {
            var json = IssueProviderSettings.LogFileContent.ToStringUsingEncoding(Encoding.UTF8, false);

            if (string.IsNullOrWhiteSpace(json)) return Enumerable.Empty<IIssue>();

            TerraformValidationResult results = null;

            try
            {
                results = JsonConvert.DeserializeObject<TerraformValidationResult>(json);
            }
            catch
            {
                Log.Error("Unable to parse json results.\n{0}", json);
                throw;
            }

            if (results.IsValid) return Enumerable.Empty<IIssue>();

            return results.Diagnostics.Select(x =>
            {
                var identifier = x.Range != null
                    ? ($"{x.Summary}:{x.Range.Filename}")
                    : x.Summary;

                var builder = IssueBuilder.NewIssue(identifier, x.Detail ?? x.Summary, this)
                    .OfRule(x.Summary)
                    .WithPriority(x.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
                        ? IssuePriority.Error
                        : IssuePriority.Warning);

                if (x.Range != null)
                {
                    builder.InFile(x.Range.Filename, x.Range.Start.Line, x.Range.End.Line);
                }

                return builder.Create();
            });
        }

        public override string ProviderName => "terraform";
    }
}