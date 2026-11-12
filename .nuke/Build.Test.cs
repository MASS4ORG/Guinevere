namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for testing and coverage reporting.
/// </summary>
sealed partial class Build
{
    [Parameter("skip-coverage (default: false): run the tests without instrumenting them; TestReport needs coverage")]
    readonly bool SkipCoverage;

    // Where Test writes the TRX reports and, unless skipped, the coverage.
    static AbsolutePath TestDirectory => TransientDirectory / "tests";
    static AbsolutePath CoverageResultFile => TestDirectory / "coverage.xml";

    // Where dotnet test writes the cobertura report it merges from all test projects.
    static AbsolutePath CoverageMergedDirectory => TestDirectory / "merged";
    static AbsolutePath CoverageReportDirectory => TestDirectory / "report";
    static AbsolutePath CoverageReportSummaryDirectory => CoverageReportDirectory / "Summary.txt";
    static AbsolutePath CoverageSettingsFile => RootDirectory / "CodeCoverage.runsettings";

    /// <summary>
    /// Runs the tests and writes their TRX reports, with coverage unless <see cref="SkipCoverage"/> is set. A project
    /// may narrow what runs through <see cref="ConfigureTests"/>.
    /// </summary>
    public Target Test => td =>
        td
            .DependsOn(Compile)
            .Produces(CoverageResultFile)
            .Executes(() =>
            {
                // Every test project writes its own uniquely named report; a shared --coverage-output would let the
                // parallel test hosts overwrite each other.
                TestDirectory.GlobFiles("*.cobertura.xml").ForEach(static report => report.DeleteFile());
                CoverageMergedDirectory.DeleteDirectory();
                var settings = WithMsBuildProperties(new DotNetTestSettings()
                    .SetConfiguration(Config)
                    .SetProcessWorkingDirectory(Solution.Directory)
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .AddProcessAdditionalArguments(
                        TestReportArgument,
                        "--minimum-expected-tests", "1",
                        "--results-directory", TestDirectory));
                if (!SkipCoverage)
                {
                    settings = settings.AddProcessAdditionalArguments(
                        "--coverage",
                        "--coverage-settings", CoverageSettingsFile, // Excludes source generated files
                        "--coverage-output-format", "cobertura");
                }

                ConfigureTests(ref settings);
                _ = DotNetTest(settings);

                if (!SkipCoverage)
                {
                    MergeCoverage();
                }
            });

    /// <summary>Configures project-specific test filters using the project's test framework.</summary>
    partial void ConfigureTests(ref DotNetTestSettings settings);

    /// <summary>Generates coverage reports from <see cref="Test"/> and publishes their summary to CI.</summary>
    public Target TestReport => td =>
        td
            .DependsOn(Test)
            .Requires(() => !SkipCoverage)
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

    /// <summary>Merges the coverage every test project wrote into <see cref="CoverageResultFile"/>.</summary>
    static void MergeCoverage()
    {
        var reportDirectory = CoverageMergedDirectory.DirectoryExists() ? CoverageMergedDirectory : TestDirectory;
        var reports = reportDirectory.GlobFiles("*.cobertura.xml").ToArray();
        if (reports.Length == 1)
        {
            reports[0].Move(CoverageResultFile, ExistsPolicy.FileOverwrite);
        }
        else
        {
            _ = ReportGenerator(settings => settings
                .SetReports(reports.Select(static report => report.ToString()))
                .SetTargetDirectory(CoverageMergedDirectory)
                .SetReportTypes([ReportTypes.Cobertura]));
            (CoverageMergedDirectory / "Cobertura.xml").Move(CoverageResultFile, ExistsPolicy.FileOverwrite);
        }

        CoverageMergedDirectory.DeleteDirectory();
    }
}
