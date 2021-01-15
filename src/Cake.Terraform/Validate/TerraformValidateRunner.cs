using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using System.IO;

namespace Cake.Terraform.Validate
{
    public class TerraformValidateRunner : TerraformRunner<TerraformValidateSettings>
    {
        private readonly ICakeEnvironment _environment;

        public TerraformValidateRunner(IFileSystem fileSystem, ICakeEnvironment environment,
            IProcessRunner processRunner, IToolLocator tools) : base(fileSystem, environment, processRunner, tools)
        {
            _environment = environment;
        }

        public void Run(TerraformValidateSettings settings)
        {
            var builder = new ProcessArgumentBuilder()
                .Append("validate");

            ProcessSettings processSettings = null;
            Action<IProcess> processHandler = null;

            if (settings.NoColor)
            {
                builder.Append("-no-color");
            }

            if (settings.OutputPath != null)
            {
                builder.Append("-json");

                processSettings = new ProcessSettings
                {
                    RedirectStandardOutput = true
                };

                processHandler = process =>
                {
                    var lines = process.GetStandardOutput().ToList();

                    if (lines.Any()) File.WriteAllLines(settings.OutputPath.MakeAbsolute(_environment).FullPath, lines);
                };
            }

            this.Run(settings, builder, processSettings, processHandler);
        }
    }
}
