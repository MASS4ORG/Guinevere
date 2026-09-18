using System;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Serilog;

namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for testing and coverage reporting.
/// </summary>
partial class Build
{
    AbsolutePath TestProjectDirectory => Solution.Guinevere_Tests.Directory;
    static AbsolutePath CoverageDirectory => RootDirectory / "coverage";
    static AbsolutePath CoverageResultFile => CoverageDirectory / "coverage.xml";
    static AbsolutePath CoverageReportDirectory => CoverageDirectory / "report";
    static AbsolutePath CoverageReportSummaryDirectory => CoverageReportDirectory / "Summary.txt";
    AbsolutePath CoverageSettingsFile => TestProjectDirectory / "CodeCoverage.runsettings";

    [Parameter("Minimum coverage threshold (default: 80)")] public readonly int CoverageThreshold = 80;

    private Target Test => td => td
        .After(Compile)
        .Produces(CoverageResultFile)
        .Executes(() =>
        {
            DotNetTasks.DotNetRun(settings => settings
                .SetConfiguration(Configuration)
                .SetProjectFile(Solution.Guinevere_Tests.Path)
                .SetApplicationArguments(
                    "--coverage",
                    "--coverage-settings", CoverageSettingsFile, // Excludes source generated files
                    "--coverage-output-format", "cobertura",
                    "--coverage-output", CoverageResultFile)
            );
        });

    public Target TestReport => td => td
        .DependsOn(Test)
        .Consumes(Test, CoverageResultFile)
        .Produces(CoverageReportDirectory / "**")
        .Executes(() =>
        {
            _ = CoverageReportDirectory.CreateDirectory();
            _ = ReportGeneratorTasks.ReportGenerator(s => s
                .SetTargetDirectory(CoverageReportDirectory)
                .SetReportTypes([ReportTypes.Html, ReportTypes.TextSummary])
                .SetReports(CoverageResultFile)
            );
            var summaryText = CoverageReportSummaryDirectory.ReadAllLines();
            Log.Information(string.Join(Environment.NewLine, summaryText));
        });
}
