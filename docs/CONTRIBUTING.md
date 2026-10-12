# Contributing

Use the issue and pull request templates. Keep one issue and one pull request focused on one problem.

## Issues

Search existing issues first. Bug reports need the affected version, operating system, minimal reproduction, expected result, actual result, and logs or screenshots when useful. Feature requests need the problem, proposed outcome, and any compatibility impact.

Discuss a new public API, significant feature, or architectural change in an issue before implementation.

## Pull requests

Link the issue, describe the user-visible change, and state how it was tested. Keep the pull request reviewable and avoid unrelated refactors. Update documentation, examples, and the changelog when the change requires them.

Run the relevant build target before requesting review:

~~~sh
./build.sh Test
~~~

## Style

Follow .editorconfig and the established patterns in the edited area. Keep public APIs documented, add tests for behavior changes, and use concise Markdown with no wrapped paragraphs.

Contributions are licensed under the repository's [license](LICENSE.md).
