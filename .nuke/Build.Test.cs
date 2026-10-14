namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for testing and coverage reporting.
/// </summary>
sealed partial class Build
{
    static AbsolutePath CoverageDirectory => RootDirectory / "coverage";
    static AbsolutePath CoverageResultFile => CoverageDirectory / "coverage.xml";

    // Where dotnet test writes the cobertura report it merges from all test projects.
    static AbsolutePath CoverageMergedDirectory => CoverageDirectory / "merged";
    static AbsolutePath CoverageReportDirectory => CoverageDirectory / "report";
    static AbsolutePath CoverageReportSummaryDirectory => CoverageReportDirectory / "Summary.txt";
    static AbsolutePath CoverageSettingsFile => RootDirectory / "CodeCoverage.runsettings";

    Target Test => td =>
        td
            .After(Restore, Compile)
            .Produces(CoverageResultFile)
            .Executes(() =>
            {
                // Every test project writes its own uniquely named report; a shared --coverage-output would let the
                // parallel test hosts overwrite each other.
                CoverageDirectory.GlobFiles("*.cobertura.xml").ForEach(report => report.DeleteFile());
                CoverageMergedDirectory.DeleteDirectory();
                DotNetTest(settings => WithMsBuildProperties(settings
                    .SetConfiguration(Config)
                    .AddProcessAdditionalArguments(
                        "--coverage",
                        "--coverage-settings", CoverageSettingsFile, // Excludes source generated files
                        "--coverage-output-format", "cobertura",
                        "--report-xunit-trx", // Consumed by tools/slow-tests.cs
                        "--results-directory", CoverageDirectory)));

                // dotnet test only merges when several test projects ran; a lone project leaves its own report.
                var reportDirectory = CoverageMergedDirectory.DirectoryExists() ? CoverageMergedDirectory : CoverageDirectory;
                reportDirectory.GlobFiles("*.cobertura.xml").Single().Move(CoverageResultFile, ExistsPolicy.FileOverwrite);
                CoverageMergedDirectory.DeleteDirectory();
            });

    public Target TestReport => td =>
        td
            .DependsOn(Test)
            .Consumes(Test, CoverageResultFile)
            .Executes(() =>
            {
                _ = CoverageReportDirectory.CreateDirectory();
                _ = ReportGenerator(
                    s => s
                         .SetTargetDirectory(CoverageReportDirectory)
                         .SetReportTypes([ReportTypes.Html, ReportTypes.TextSummary, ReportTypes.MarkdownSummaryGithub])
                         .SetReports(CoverageResultFile)
                );
                var summaryText = CoverageReportSummaryDirectory.ReadAllLines();
                Log.Information("{Summary}", string.Join(Environment.NewLine, summaryText));

                // GitHub Actions renders this file on the run's summary page.
                if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } stepSummary)
                {
                    File.AppendAllText(stepSummary, (CoverageReportDirectory / "SummaryGithub.md").ReadAllText());
                }
            });
}
