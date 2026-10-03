# Tools

Single-file C# helpers for .NET 10. Run from the repository root after `./build.sh test`:

```sh
dotnet run tools/crap.cs -- --base HEAD
```

The validator reports CRAP scores from `coverage/coverage.xml` for methods changed since the selected
git ref, including working-tree changes and untracked files. It exits with code 1 when a score exceeds
15. Use `--base main` for changes on a feature branch, `--max <score>` to choose a limit,
`--coverage <file>` for another report, or `--all` to inspect every method in changed files.

CRAP combines cyclomatic complexity with uncovered code: `complexity² × (1 − coverage)³ + complexity`.
Native integration methods absent from the report require separate verification; the tool reports
only methods present in coverage.
