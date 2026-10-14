namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for testing and coverage reporting.
/// </summary>
sealed partial class Build
{
    static AbsolutePath CoverageDirectory => RootDirectory / "coverage";
    static AbsolutePath CoverageResultFile => CoverageDirectory / "coverage.xml";
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
                DotNetTest(settings => WithMsBuildProperties(settings
                    .SetConfiguration(Config)
                    .AddProcessAdditionalArguments(
                        "--coverage",
                        "--coverage-settings", CoverageSettingsFile, // Excludes source generated files
                        "--coverage-output-format", "cobertura",
                        "--report-xunit-trx", // Consumed by tools/slow-tests.cs
                        "--results-directory", CoverageDirectory)));

                var merged = CoverageDirectory / "merged";
                _ = ReportGenerator(s => s
                    .SetTargetDirectory(merged)
                    .SetReportTypes(ReportTypes.Cobertura)
                    .SetReports(CoverageDirectory.GlobFiles("*.cobertura.xml").Select(static report => report.ToString())));
                (merged / "Cobertura.xml").Move(CoverageResultFile, ExistsPolicy.FileOverwrite);
                merged.DeleteDirectory();
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
