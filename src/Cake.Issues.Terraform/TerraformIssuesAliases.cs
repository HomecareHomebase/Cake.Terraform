using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;

namespace Cake.Issues.Terraform
{
    public static class TerraformIssuesAliases
    {
        [CakeMethodAlias]
        [CakeAliasCategory(IssuesAliasConstants.IssueProviderCakeAliasCategory)]
        public static IIssueProvider TerraformValidationIssues(this ICakeContext context, FilePath logFilePath)
        {
            context.NotNull(nameof(context));
            logFilePath.NotNull(nameof(context));

            return context.TerraformValidationIssues(new TerraformIssuesSettings(logFilePath));
        }

        [CakeMethodAlias]
        [CakeAliasCategory(IssuesAliasConstants.IssueProviderCakeAliasCategory)]
        public static IIssueProvider TerraformValidationIssues(this ICakeContext context,
            TerraformIssuesSettings settings)
        {
            context.NotNull(nameof(context));
            settings.NotNull(nameof(settings));

            return new TerraformIssuesProvider(context.Log, settings);
        }
    }
}
